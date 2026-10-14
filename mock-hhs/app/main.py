"""FinCopilot Mock HHS — ÖHVPS v2.0.0 Hesap Bilgisi Hizmeti simülasyonu."""
from contextlib import asynccontextmanager

from fastapi import FastAPI
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse

from . import config
from .db import init_db
from .errors import OhvpsError, ohvps_error_handler
from .routers import consent, gkd
from .seed import BANKS, TEST_USERS

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


@app.get("/_mock/health", tags=["Mock yardımcı"])
def health():
    return {"status": "ok"}


@app.get("/_mock/info", tags=["Mock yardımcı"])
def info():
    """Geliştirme için: tanımlı bankalar, YÖS kodları ve test kullanıcıları."""
    return {
        "bankalar": [{"hhsKod": k, "ad": v["name"]} for k, v in BANKS.items()],
        "yosKodlari": sorted(config.ALLOWED_YOS_CODES),
        "testKullanicilari": [{"tckn": k, "ad": v["name"], "sifre": v["password"]} for k, v in TEST_USERS.items()],
        "testSmsKodu": config.TEST_OTP,
    }
