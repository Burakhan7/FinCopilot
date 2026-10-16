"""Geliştirme yardımcıları: tarayıcıda test için sahte YÖS geri dönüş sayfası.

Gerçek uygulamada banka, kullanıcıyı mobil uygulamaya (deep link) döndürür.
Bilgisayarda test ederken yonAdr olarak bu sayfa verilir:
    http://localhost:8090/_mock/callback
Sayfa gelen parametreleri gösterir ve demo betiği için saklar.
"""
from html import escape

from fastapi import APIRouter, Request
from fastapi.responses import HTMLResponse, JSONResponse

IPTAL_KODLARI = {
    "01": "Yeni rıza talebi ile iptal",
    "02": "Kullanıcı isteğiyle banka üzerinden iptal",
    "03": "Kullanıcı isteğiyle uygulama üzerinden iptal",
    "04": "Süre aşımı: onay beklenirken",
    "05": "Süre aşımı: onaylandı ama token alınmadı",
    "07": "Aynı rıza ile mükerrer çağrı",
    "08": "Bankaya girilen TCKN, rızadaki TCKN ile uyuşmuyor",
    "09": "Kullanıcının bu bankada uygun hesap/kartı yok",
    "13": "Kullanıcı bankada vazgeçti",
    "14": "Fraud şüphesi",
    "99": "Diğer",
}

router = APIRouter(prefix="/_mock", tags=["Mock yardımcı"])

_callbacks: dict[str, dict] = {}


@router.get("/callback", response_class=HTMLResponse)
def callback(request: Request):
    params = dict(request.query_params)
    riza = params.get("rizaNo")
    if riza:
        _callbacks[riza] = params
    ok = "yetKod" in params
    title = "Onay alındı ✅" if ok else "Onay alınamadı ❌"
    rows = "".join(f"<tr><td>{escape(k)}</td><td><code>{escape(v)}</code></td></tr>" for k, v in params.items())
    kod = params.get("rizaIptDtyKod", "")
    note = ("Bu sayfa FinCopilot uygulamasının yerini tutuyor. Demo betiği yetKod'u buradan otomatik alacak; "
            "terminale dönebilirsin." if ok else
            f"Rıza iptal edildi. Sebep ({escape(kod)}): <b>{escape(IPTAL_KODLARI.get(kod, 'bilinmiyor'))}</b>")
    return f"""<!doctype html><html lang="tr"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1"><title>FinCopilot – geri dönüş</title>
<style>body{{font-family:-apple-system,Segoe UI,Roboto,sans-serif;background:#f2f4f7;margin:0;padding:24px}}
.c{{max-width:560px;margin:auto;background:#fff;border-radius:12px;padding:20px;box-shadow:0 1px 3px rgba(0,0,0,.08)}}
td{{padding:6px 8px;border-bottom:1px solid #eee;font-size:14px;word-break:break-all}}</style></head>
<body><div class="c"><h2>{title}</h2><p>{note}</p><table>{rows}</table></div></body></html>"""


@router.get("/callback/{rizaNo}")
def callback_result(rizaNo: str):
    data = _callbacks.get(rizaNo)
    if data is None:
        return JSONResponse({"durum": "bekleniyor"}, status_code=404)
    return data


# ------------------------------------------------------------------ test YÖS olay dinleyicisi
_received: list[dict] = []


@router.post("/yos/olay-dinleme", status_code=202)
async def yos_listener(request: Request):
    """FinCopilot backend'i yokken olayları karşılayan test dinleyicisi (YÖS'ün POST /olay-dinleme ucu)."""
    _received.append(await request.json())
    return {}


@router.get("/yos/olaylar")
def yos_received(rizaNo: str | None = None, kaynakNo: str | None = None):
    items = _received
    if rizaNo:
        items = [e for e in items if e.get("kaynakNo") == rizaNo]
    if kaynakNo:
        items = [e for e in items if e.get("kaynakNo") == kaynakNo]
    return items


# ------------------------------------------------------------------ simülasyon tetikleyicileri
from datetime import datetime as _dt  # noqa: E402
from decimal import Decimal as _D  # noqa: E402
from typing import Literal as _Literal  # noqa: E402

from fastapi import Depends  # noqa: E402
from pydantic import BaseModel, Field  # noqa: E402
from sqlalchemy.orm import Session  # noqa: E402

from .. import consent_service as _svc  # noqa: E402
from .. import errors as _errors  # noqa: E402
from ..db import Consent as _Consent, InjectedTx, get_db  # noqa: E402
from ..generator.personas import PERSONAS as _PERSONAS  # noqa: E402


class YeniIslem(BaseModel):
    tckn: str
    hspRef: str
    tutar: _D = Field(gt=0)
    brcAlc: _Literal["A", "B"] = "A"
    islTur: str = "EFT"
    islAmc: str | None = "05"
    aciklama: str = "EFT - DORUK YAZILIM TEKNOLOJI AS - MAAS ODEMESI"
    karsiUnvan: str | None = "DORUK YAZILIM TEKNOLOJI AS"


@router.post("/simulasyon/islem", status_code=201)
def inject_tx(req: YeniIslem, db: Session = Depends(get_db)):
    """Şu an gerçekleşmiş gibi yeni bir hesap hareketi ekler (örn. 'maaş yattı').
    06 izinli ve BAKIYE aboneliği olan rızalar için birkaç saniye içinde olay gönderilir."""
    p = _PERSONAS.get(req.tckn)
    if p is None or req.hspRef not in {a["ref"] for a in p["accounts"]}:
        raise _errors.resource_not_found("Kullanıcı veya hesap bulunamadı.")
    row = InjectedTx(tckn=req.tckn, hsp_ref=req.hspRef, zaman=_svc.now(), tutar=str(req.tutar), brc_alc=req.brcAlc,
                     isl_tur=req.islTur, isl_amc=req.islAmc, aciklama=req.aciklama, karsi_unvan=req.karsiUnvan)
    db.add(row)
    db.commit()
    return {"id": row.id, "zaman": row.zaman.isoformat()}


@router.post("/simulasyon/riza-iptal/{rizaNo}", status_code=200)
def bank_side_revoke(rizaNo: str, db: Session = Depends(get_db)):
    """Kullanıcı rızayı bankanın kendi kanalından iptal etmiş gibi (kod 02)."""
    c = db.get(_Consent, rizaNo)
    if c is None:
        raise _errors.consent_not_found()
    if c.riza_drm in ("S", "I"):
        raise _errors.consent_revoked()
    _svc.cancel(db, c, "02")
    return {"rizaNo": rizaNo, "rizaDrm": c.riza_drm, "rizaIptDtyKod": c.riza_ipt_dty_kod}


@router.post("/simulasyon/olay-tick")
async def force_tick():
    """Olay döngüsünü beklemeden bir tur çalıştırır (tarama + iletim)."""
    from .. import events as _events
    await _events.tick()
    return {"ok": True, "zaman": _dt.now().isoformat(timespec="seconds")}
