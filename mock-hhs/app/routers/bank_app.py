"""Sahte banka mobil uygulaması (ayrık GKD).

Ayrık akışta kullanıcı FinCopilot'tan çıkmaz; banka kendi mobil uygulamasına
"onay bekleyen isteğiniz var" bildirimi gönderir. Bu sayfa o uygulamanın yerini tutar:
    http://localhost:8090/banka-uygulamasi?hhsKod=9001
Kullanıcı giriş yapar, bekleyen istekleri görür, hesap/kart seçip onaylar.
Sonuç YÖS'e AYRIK_GKD_BASARILI / AYRIK_GKD_BASARISIZ olayıyla bildirilir.
"""
import secrets

from fastapi import APIRouter, Depends, Form, Request
from fastapi.responses import HTMLResponse, RedirectResponse
from sqlalchemy import select
from sqlalchemy.orm import Session

from .. import consent_service as svc
from ..db import AppSession, Consent, get_db
from ..seed import BANKS, TEST_USERS, products_for
from .gkd import templates

router = APIRouter(prefix="/banka-uygulamasi", include_in_schema=False)


def _user_matches(c: Consent, tckn: str) -> bool:
    a = c.ayrik_gkd or {}
    if a.get("ohkTanimTip") == "TCKN":
        return a.get("ohkTanimDeger") == tckn and c.kmlk_vrs == tckn
    return c.kmlk_vrs == tckn


def _pending(db: Session, tckn: str, hhs: str) -> list[Consent]:
    rows = db.scalars(select(Consent).where(Consent.hhs_kod == hhs, Consent.yet_yntm == "A", Consent.riza_drm == "B")).all()
    out = []
    for c in rows:
        svc.apply_timeouts(db, c)
        if c.riza_drm == "B" and _user_matches(c, tckn):
            out.append(c)
    return out


def _ctx(request, hhs, **kw):
    return {"request": request, "bank": BANKS[hhs], "hhs": hhs, **kw}


@router.get("", response_class=HTMLResponse)
def home(request: Request, hhsKod: str = "9001"):
    if hhsKod not in BANKS:
        hhsKod = "9001"
    return templates.TemplateResponse(request, "app_login.html", _ctx(request, hhsKod, error=None, banks=BANKS))


@router.post("/giris")
def login(request: Request, hhsKod: str = Form(...), tckn: str = Form(...), sifre: str = Form(...),
          db: Session = Depends(get_db)):
    user = TEST_USERS.get(tckn)
    if hhsKod not in BANKS or user is None or user["password"] != sifre:
        return templates.TemplateResponse(request, "app_login.html", _ctx(request, hhsKod if hhsKod in BANKS else "9001",
                                          error="TCKN veya şifre hatalı.", banks=BANKS), status_code=401)
    s = AppSession(sid=secrets.token_urlsafe(16), tckn=tckn, hhs_kod=hhsKod, created_at=svc.now())
    db.add(s)
    db.commit()
    return RedirectResponse(f"/banka-uygulamasi/istekler?sid={s.sid}", status_code=303)


@router.get("/istekler", response_class=HTMLResponse)
def requests_page(request: Request, sid: str, db: Session = Depends(get_db)):
    s = db.get(AppSession, sid)
    if s is None:
        return RedirectResponse("/banka-uygulamasi", status_code=303)
    items = []
    for c in _pending(db, s.tckn, s.hhs_kod):
        p = products_for(s.tckn, s.hhs_kod)
        items.append({"riza": c, "accounts": p["accounts"] if "01" in c.izin_tur else [],
                      "cards": p["cards"] if "07" in c.izin_tur else []})
    return templates.TemplateResponse(request, "app_requests.html", _ctx(
        request, s.hhs_kod, sid=sid, user=TEST_USERS[s.tckn], items=items, msg=request.query_params.get("msg")))


@router.post("/onay")
def decide(sid: str = Form(...), rizaNo: str = Form(...), islem: str = Form(...),
           hspRef: list[str] = Form(default=[]), kartRef: list[str] = Form(default=[]),
           db: Session = Depends(get_db)):
    s = db.get(AppSession, sid)
    if s is None:
        return RedirectResponse("/banka-uygulamasi", status_code=303)
    c = db.get(Consent, rizaNo)
    if c is None or c not in _pending(db, s.tckn, s.hhs_kod):
        return RedirectResponse(f"/banka-uygulamasi/istekler?sid={sid}&msg=istek-gecersiz", status_code=303)
    if islem != "onay":
        svc.cancel(db, c, "13")
        return RedirectResponse(f"/banka-uygulamasi/istekler?sid={sid}&msg=reddedildi", status_code=303)
    p = products_for(s.tckn, s.hhs_kod)
    hesaplar = [h for h in hspRef if h in {a["hspRef"] for a in p["accounts"]}]
    kartlar = [k for k in kartRef if k in {k["kartRef"] for k in p["cards"]}]
    if not hesaplar and not kartlar:
        return RedirectResponse(f"/banka-uygulamasi/istekler?sid={sid}&msg=secim-yok", status_code=303)
    svc.authorize(db, c, hesaplar, kartlar)
    return RedirectResponse(f"/banka-uygulamasi/istekler?sid={sid}&msg=onaylandi", status_code=303)
