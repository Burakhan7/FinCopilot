"""Hesap bilgisi rızası ve erişim belirteci uçları."""
from fastapi import APIRouter, Depends, Header, Response
from sqlalchemy.orm import Session

from .. import consent_service as svc
from .. import errors
from ..db import get_db
from ..schemas import ErisimBelirteci, ErisimBelirteciIstegi, HesapBilgisiRizasi, HesapBilgisiRizasiIstegi

router = APIRouter(tags=["Hesap Bilgisi Rızası"])


def common_headers(
    response: Response,
    x_request_id: str = Header(..., alias="X-Request-ID"),
    x_aspsp_code: str = Header(..., alias="X-ASPSP-Code"),
    x_tpp_code: str = Header(..., alias="X-TPP-Code"),
    x_jws_signature: str | None = Header(None, alias="X-JWS-Signature"),  # mock: doğrulanmaz
):
    response.headers["X-Request-ID"] = x_request_id
    return {"hhs": x_aspsp_code, "yos": x_tpp_code}


def _check_owner(c, h):
    if c.hhs_kod != h["hhs"]:
        raise errors.invalid_aspsp()
    if c.yos_kod != h["yos"]:
        raise errors.invalid_tpp()


@router.post("/hesap-bilgisi-rizasi", status_code=201, response_model=HesapBilgisiRizasi, response_model_exclude_none=True)
def create(req: HesapBilgisiRizasiIstegi, h=Depends(common_headers), db: Session = Depends(get_db)):
    svc.validate_request(req, h["hhs"], h["yos"])
    c = svc.create_consent(db, req)
    return svc.to_response(c)


@router.get("/hesap-bilgisi-rizasi/{rizaNo}", response_model=HesapBilgisiRizasi, response_model_exclude_none=True)
def read(rizaNo: str, h=Depends(common_headers), db: Session = Depends(get_db)):
    c = svc.get_consent(db, rizaNo)
    _check_owner(c, h)
    return svc.to_response(c)


@router.delete("/hesap-bilgisi-rizasi/{rizaNo}", status_code=204)
def revoke(
    rizaNo: str,
    h=Depends(common_headers),
    x_access_token: str | None = Header(None, alias="X-Access-Token"),
    db: Session = Depends(get_db),
):
    c = svc.get_consent(db, rizaNo)
    _check_owner(c, h)
    if c.riza_drm in ("S", "I"):
        raise errors.consent_revoked()
    if c.riza_drm == "K":
        svc.require_access_token(db, x_access_token, rizaNo)
    svc.cancel(db, c, "03")  # Kullanıcı isteği ile YÖS üzerinden iptal
    return Response(status_code=204)


@router.post("/erisim-belirteci", response_model=ErisimBelirteci, tags=["Erişim Belirteci"])
def token(req: ErisimBelirteciIstegi, h=Depends(common_headers), db: Session = Depends(get_db)):
    if req.rizaTip != "H":
        raise errors.invalid_field("Bu mock yalnızca hesap bilgisi rızasını (rizaTip=H) destekler.")
    c = svc.get_consent(db, req.rizaNo)
    _check_owner(c, h)
    if req.yetTip == "yet_kod":
        if not req.yetKod:
            raise errors.invalid_field("yetTip=yet_kod için yetKod zorunlu.")
        tok = svc.issue_token_from_code(db, req.rizaNo, req.yetKod)
    else:
        if not req.yenilemeBelirteci:
            raise errors.invalid_field("yetTip=yenileme_belirteci için yenilemeBelirteci zorunlu.")
        tok = svc.issue_token_from_refresh(db, req.rizaNo, req.yenilemeBelirteci)
    t = svc.now()
    return ErisimBelirteci(
        erisimBelirteci=tok.access_token,
        gecerlilikSuresi=int((svc.aware(tok.access_expires_at) - t).total_seconds()),
        yenilemeBelirteci=tok.refresh_token,
        yenilemeBelirteciGecerlilikSuresi=int((svc.aware(tok.refresh_expires_at) - t).total_seconds()),
    )
