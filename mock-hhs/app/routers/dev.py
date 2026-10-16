"""Geliştirme yardımcıları: tarayıcıda test için sahte YÖS geri dönüş sayfası.

Gerçek uygulamada banka, kullanıcıyı mobil uygulamaya (deep link) döndürür.
Bilgisayarda test ederken yonAdr olarak bu sayfa verilir:
    http://localhost:8090/_mock/callback
Sayfa gelen parametreleri gösterir ve demo betiği için saklar.
"""
from html import escape

from fastapi import APIRouter, Request
from fastapi.responses import HTMLResponse, JSONResponse

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
    note = ("Bu sayfa FinCopilot uygulamasının yerini tutuyor. Demo betiği yetKod'u buradan otomatik alacak; "
            "terminale dönebilirsin." if ok else "Rıza iptal edildi. rizaIptDtyKod alanı sebebini gösterir.")
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
