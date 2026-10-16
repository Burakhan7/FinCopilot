"""FinCopilot Mock HHS — ÖHVPS v2.0.0 Hesap Bilgisi Hizmeti simülasyonu."""
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse

from . import config
from .db import init_db
from .errors import OhvpsError, ohvps_error_handler
from .generator.personas import PERSONAS
from .routers import consent, data, dev, gkd
from .seed import BANKS

@asynccontextmanager
async def lifespan(_app):
    init_db()
    yield


app = FastAPI(
    lifespan=lifespan,
    title="FinCopilot Mock HHS",
    version="0.1.0",
    description="TCMB ÖHVPS v2.0.0 Hesap Bilgisi Hizmeti uçlarının bitirme projesi için simülasyonu. "
                "Bankalar ve kullanıcılar kurgusaldır.",
)

app.add_exception_handler(OhvpsError, ohvps_error_handler)


@app.exception_handler(RequestValidationError)
async def validation_handler(request, exc: RequestValidationError):
    return JSONResponse(
        status_code=400,
        content={
            "path": request.url.path,
            "httpCode": 400,
            "errorCode": "TR.OHVPS.Field.Invalid",
            "moreInformationTr": "İstek alanları geçersiz.",
            "fieldErrors": [{"field": ".".join(map(str, e["loc"])), "message": e["msg"]} for e in exc.errors()],
        },
    )


app.include_router(consent.router)
app.include_router(gkd.router)
app.include_router(data.router)
app.include_router(dev.router)


@app.get("/_mock/health", tags=["Mock yardımcı"])
def health():
    return {"status": "ok"}


@app.get("/_mock/info", tags=["Mock yardımcı"])
def info():
    """Geliştirme için: tanımlı bankalar, YÖS kodları ve test kullanıcıları."""
    return {
        "bankalar": [{"hhsKod": k, "ad": v["name"]} for k, v in BANKS.items()],
        "yosKodlari": sorted(config.ALLOWED_YOS_CODES),
        "testKullanicilari": [
            {"tckn": k, "ad": p["name"], "sifre": p["password"], "dogumTarihi": p["birth_date"], "telefon": p["phone"],
             "sehir": p["city"], "meslek": p["job"], "beyanEdilenAylikGelir": str(p["declared_monthly_income"]),
             "bankalar": sorted({a["bank"] for a in p["accounts"]} | {c["bank"] for c in p["cards"]}),
             "hesaplar": [{"hspRef": a["ref"], "hhsKod": a["bank"], "urun": a["product"], "prBrm": a["currency"],
                           "kmhLimit": str(a["kmh_limit"])} for a in p["accounts"]],
             "kartlar": [{"kartRef": c["ref"], "hhsKod": c["bank"], "urun": c["product"], "tip": c["kartTipi"],
                          "limit": str(c.get("limit", "")), "hesapKesimGunu": c.get("statement_day")} for c in p["cards"]]}
            for k, p in PERSONAS.items()],
        "testSmsKodu": config.TEST_OTP,
    }
