"""ÖHVPS tarzı hata yanıtları.

Not: errorCode adları ÖHVPS dokümanındaki örneklerden alındı
(TR.OHVPS.Connection.InvalidASPSP vb.). Yanıt gövdesinin alan adları
spesifikasyondaki hata nesnesiyle ayrıca karşılaştırılmalı.
"""
from datetime import datetime

from fastapi import Request
from fastapi.responses import JSONResponse

from .config import TR_TZ


class OhvpsError(Exception):
    def __init__(self, http_code: int, error_code: str, message: str):
        self.http_code = http_code
        self.error_code = error_code
        self.message = message


async def ohvps_error_handler(request: Request, exc: OhvpsError) -> JSONResponse:
    return JSONResponse(
        status_code=exc.http_code,
        content={
            "path": request.url.path,
            "timestamp": datetime.now(TR_TZ).isoformat(timespec="seconds"),
            "httpCode": exc.http_code,
            "errorCode": exc.error_code,
            "moreInformationTr": exc.message,
        },
        headers={"X-Request-ID": request.headers.get("X-Request-ID", "")},
    )


# Sık kullanılanlar
def invalid_aspsp(msg="HHS kodu geçersiz veya başlıkla uyuşmuyor."):
    return OhvpsError(400, "TR.OHVPS.Connection.InvalidASPSP", msg)


def invalid_tpp(msg="YÖS kodu geçersiz veya başlıkla uyuşmuyor."):
    return OhvpsError(400, "TR.OHVPS.Connection.InvalidTPP", msg)


def incorrect_permission(msg):
    return OhvpsError(400, "TR.OHVPS.Business.IncorrectPermissionType", msg)


def invalid_time(msg):
    return OhvpsError(400, "TR.OHVPS.Business.InvalidStartEndTime", msg)


def invalid_field(msg):
    return OhvpsError(400, "TR.OHVPS.Field.Invalid", msg)


def consent_not_found():
    return OhvpsError(404, "TR.OHVPS.Resource.NotFound", "Rıza bulunamadı.")


def consent_revoked():
    return OhvpsError(403, "TR.OHVPS.Resource.ConsentRevoked", "Rıza iptal edilmiş veya sonlanmış.")


def unauthorized(msg="Erişim belirteci geçersiz veya süresi dolmuş."):
    return OhvpsError(401, "TR.OHVPS.Connection.InvalidToken", msg)
