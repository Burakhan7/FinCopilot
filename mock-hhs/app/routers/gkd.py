"""Sahte banka GKD sayfası (yönlendirmeli akış).

Akış: giriş (TCKN + şifre) → SMS kodu → hesap/kart seçimi → onay
→ YÖS'ün yonAdr adresine yetKod ile geri yönlendirme.

drmKod: YÖS, hhsYonAdr adresine `?drmKod=...` ekleyerek gelir; banka bu
değeri geri dönüşte aynen iletir (CSRF koruması YÖS tarafında doğrulanır).
"""
import secrets
from urllib.parse import urlencode

from fastapi import APIRouter, Depends, Form, Request
from fastapi.responses import HTMLResponse, RedirectResponse
from fastapi.templating import Jinja2Templates
from sqlalchemy.orm import Session

from .. import config
from .. import consent_service as svc
from ..db import GkdSession, get_db
from ..seed import BANKS, TEST_USERS, products_for

router = APIRouter(prefix="/gkd", tags=["Sahte Banka GKD"], include_in_schema=False)
templates = Jinja2Templates(directory=str(__import__("pathlib").Path(__file__).parent.parent / "templates"))


def _redirect_back(c, drm_kod: str | None, **params) -> RedirectResponse:
    q = {"rizaNo": c.riza_no, **params}
    if drm_kod:
        q["drmKod"] = drm_kod
    sep = "&" if "?" in c.yon_adr else "?"
    return RedirectResponse(f"{c.yon_adr}{sep}{urlencode(q)}", status_code=303)


def _ctx(request, c, **kw):
    bank = BANKS[c.hhs_kod]
    return {"request": request, "bank": bank, "riza": c, **kw}


def _session(db: Session, sid: str, riza_no: str, step: str) -> GkdSession | None:
    s = db.get(GkdSession, sid)
    if s is None or s.riza_no != riza_no or s.step != step:
        return None
    return s


@router.get("/{rizaNo}", response_class=HTMLResponse)
def login_page(request: Request, rizaNo: str, drmKod: str | None = None, db: Session = Depends(get_db)):
    c = svc.get_consent(db, rizaNo)
    if c.riza_drm != "B":
        return templates.TemplateResponse(request, "message.html", _ctx(request, c, title="İşlem geçersiz",
                                          text="Bu onay isteği artık geçerli değil."), status_code=400)
    s = GkdSession(sid=secrets.token_urlsafe(16), riza_no=rizaNo, drm_kod=drmKod, step="login", created_at=svc.now())
    db.add(s)
    db.commit()
    return templates.TemplateResponse(request, "login.html", _ctx(request, c, sid=s.sid, error=None))


@router.post("/{rizaNo}/giris", response_class=HTMLResponse)
def login_submit(request: Request, rizaNo: str, sid: str = Form(...), tckn: str = Form(...),
                 sifre: str = Form(...), db: Session = Depends(get_db)):
    c = svc.get_consent(db, rizaNo)
    s = _session(db, sid, rizaNo, "login")
    if s is None or c.riza_drm != "B":
        return templates.TemplateResponse(request, "message.html", _ctx(request, c, title="Oturum geçersiz",
                                          text="Lütfen uygulamaya dönüp tekrar deneyin."), status_code=400)
    user = TEST_USERS.get(tckn)
    if user is None or user["password"] != sifre:
        return templates.TemplateResponse(request, "login.html",
                                          _ctx(request, c, sid=sid, error="TCKN veya şifre hatalı."), status_code=401)
    if tckn != c.kmlk_vrs:
        svc.cancel(db, c, "08")  # GKD iptali: rızaNo ile TCKN uyuşmaması
        return _redirect_back(c, s.drm_kod, rizaDrm="I", rizaIptDtyKod="08")
    s.step = "otp"
    db.commit()
    return templates.TemplateResponse(request, "otp.html", _ctx(request, c, sid=sid, error=None, test_otp=config.TEST_OTP))


@router.post("/{rizaNo}/otp", response_class=HTMLResponse)
def otp_submit(request: Request, rizaNo: str, sid: str = Form(...), otp: str = Form(...), db: Session = Depends(get_db)):
    c = svc.get_consent(db, rizaNo)
    s = _session(db, sid, rizaNo, "otp")
    if s is None or c.riza_drm != "B":
        return templates.TemplateResponse(request, "message.html", _ctx(request, c, title="Oturum geçersiz",
                                          text="Lütfen uygulamaya dönüp tekrar deneyin."), status_code=400)
    if otp != config.TEST_OTP:
        return templates.TemplateResponse(request, "otp.html", _ctx(request, c, sid=sid, error="SMS kodu hatalı.",
                                          test_otp=config.TEST_OTP), status_code=401)
    s.step = "select"
    db.commit()
    p = products_for(c.kmlk_vrs, c.hhs_kod)
    accounts = p["accounts"] if "01" in c.izin_tur else []
    cards = p["cards"] if "07" in c.izin_tur else []
    if not accounts and not cards:
        svc.cancel(db, c, "09")  # GKD iptali: uygun ürünü bulunmuyor
        return _redirect_back(c, s.drm_kod, rizaDrm="I", rizaIptDtyKod="09")
    return templates.TemplateResponse(request, "select.html", _ctx(request, c, sid=sid, accounts=accounts,
                                      cards=cards, user=TEST_USERS[c.kmlk_vrs], error=None))


@router.post("/{rizaNo}/onay")
def approve(request: Request, rizaNo: str, sid: str = Form(...), islem: str = Form(...),
            hspRef: list[str] = Form(default=[]), kartRef: list[str] = Form(default=[]),
            db: Session = Depends(get_db)):
    c = svc.get_consent(db, rizaNo)
    s = _session(db, sid, rizaNo, "select")
    if s is None or c.riza_drm != "B":
        return templates.TemplateResponse(request, "message.html", _ctx(request, c, title="Oturum geçersiz",
                                          text="Lütfen uygulamaya dönüp tekrar deneyin."), status_code=400)
    s.step = "done"
    db.commit()
    if islem != "onay":
        svc.cancel(db, c, "13")  # GKD iptali: ÖHK isteği ile vazgeçildi
        return _redirect_back(c, s.drm_kod, rizaDrm="I", rizaIptDtyKod="13")

    p = products_for(c.kmlk_vrs, c.hhs_kod)
    valid_acc = {a["hspRef"] for a in p["accounts"]}
    valid_card = {k["kartRef"] for k in p["cards"]}
    hesaplar = [h for h in hspRef if h in valid_acc]
    kartlar = [k for k in kartRef if k in valid_card]
    if not hesaplar and not kartlar:
        s.step = "select"
        db.commit()
        return templates.TemplateResponse(request, "select.html", _ctx(
            request, c, sid=sid, accounts=p["accounts"] if "01" in c.izin_tur else [],
            cards=p["cards"] if "07" in c.izin_tur else [], user=TEST_USERS[c.kmlk_vrs],
            error="En az bir hesap veya kart seçmelisiniz."), status_code=400)

    yet_kod = svc.authorize(db, c, hesaplar, kartlar)
    return _redirect_back(c, s.drm_kod, yetKod=yet_kod)
