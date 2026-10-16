"""Olay abonelik servisleri (/olay-abonelik)."""
import uuid
from typing import Literal

from fastapi import APIRouter, Depends, Header, Query, Response
from pydantic import BaseModel, Field
from sqlalchemy import select
from sqlalchemy.orm import Session

from .. import errors, events
from ..config import TR_TZ
from ..db import Event, Subscription, get_db
from ..schemas import KatilimciBlg
from ..seed import BANKS
from .. import config

router = APIRouter(tags=["Olay Bildirim"])

OlayTipi = Literal["KAYNAK_GUNCELLENDI", "AYRIK_GKD_BASARILI", "AYRIK_GKD_BASARISIZ"]
KaynakTipi = Literal["HESAP_BILGISI_RIZASI", "BAKIYE", "KART_BILGILERI"]
VALID = {
    ("KAYNAK_GUNCELLENDI", "HESAP_BILGISI_RIZASI"), ("KAYNAK_GUNCELLENDI", "BAKIYE"),
    ("KAYNAK_GUNCELLENDI", "KART_BILGILERI"),
    ("AYRIK_GKD_BASARILI", "HESAP_BILGISI_RIZASI"), ("AYRIK_GKD_BASARISIZ", "HESAP_BILGISI_RIZASI"),
}


class AbonelikTipi(BaseModel):
    olayTipi: OlayTipi
    kaynakTipi: KaynakTipi


class OlayAbonelikIstegi(BaseModel):
    katilimciBlg: KatilimciBlg
    abonelikTipleri: list[AbonelikTipi] = Field(min_length=1)


def headers(response: Response,
            x_request_id: str = Header(..., alias="X-Request-ID"),
            x_aspsp_code: str = Header(..., alias="X-ASPSP-Code"),
            x_tpp_code: str = Header(..., alias="X-TPP-Code")):
    response.headers["X-Request-ID"] = x_request_id
    if x_aspsp_code not in BANKS:
        raise errors.invalid_aspsp()
    if x_tpp_code not in config.ALLOWED_YOS_CODES:
        raise errors.invalid_tpp()
    return {"hhs": x_aspsp_code, "yos": x_tpp_code}


def out(s: Subscription) -> dict:
    return {
        "olayAbonelikNo": s.olay_abonelik_no,
        "olusturmaZamani": events.aware(s.olus_zmn).astimezone(TR_TZ).isoformat(timespec="seconds"),
        "guncellemeZamani": events.aware(s.gncl_zmn).astimezone(TR_TZ).isoformat(timespec="seconds"),
        "katilimciBlg": {"hhsKod": s.hhs_kod, "yosKod": s.yos_kod},
        "abonelikTipleri": s.abonelik_tipleri,
    }


def _validate(req: OlayAbonelikIstegi, h):
    if req.katilimciBlg.hhsKod != h["hhs"]:
        raise errors.invalid_aspsp()
    if req.katilimciBlg.yosKod != h["yos"]:
        raise errors.invalid_tpp()
    for t in req.abonelikTipleri:
        if (t.olayTipi, t.kaynakTipi) not in VALID:
            raise errors.invalid_field(f"Geçersiz olay/kaynak tipi eşleşmesi: {t.olayTipi}/{t.kaynakTipi}")


def _get(db: Session, no: str, h) -> Subscription:
    s = db.get(Subscription, no)
    if s is None or s.hhs_kod != h["hhs"] or s.yos_kod != h["yos"]:
        raise errors.resource_not_found("Olay aboneliği bulunamadı.")
    return s


@router.post("/olay-abonelik", status_code=201)
def create(req: OlayAbonelikIstegi, h=Depends(headers), db: Session = Depends(get_db)):
    _validate(req, h)
    if db.scalars(select(Subscription).where(Subscription.hhs_kod == h["hhs"], Subscription.yos_kod == h["yos"])).first():
        raise errors.OhvpsError(400, "TR.OHVPS.Business.EventSubscriptionAlreadyExists",
                                "Bu HHS'de zaten bir olay aboneliği var; PUT ile güncelleyin.")
    t = events.now()
    s = Subscription(olay_abonelik_no=str(uuid.uuid4()), hhs_kod=h["hhs"], yos_kod=h["yos"],
                     abonelik_tipleri=[x.model_dump() for x in req.abonelikTipleri], olus_zmn=t, gncl_zmn=t)
    db.add(s)
    db.commit()
    return out(s)


@router.get("/olay-abonelik")
def read(h=Depends(headers), db: Session = Depends(get_db)):
    s = db.scalars(select(Subscription).where(Subscription.hhs_kod == h["hhs"], Subscription.yos_kod == h["yos"])).first()
    if s is None:
        raise errors.resource_not_found("Olay aboneliği bulunamadı.")
    return out(s)


@router.put("/olay-abonelik/{olayAbonelikNo}")
def update(olayAbonelikNo: str, req: OlayAbonelikIstegi, h=Depends(headers), db: Session = Depends(get_db)):
    _validate(req, h)
    s = _get(db, olayAbonelikNo, h)
    s.abonelik_tipleri = [x.model_dump() for x in req.abonelikTipleri]
    s.gncl_zmn = events.now()
    db.commit()
    return out(s)


@router.delete("/olay-abonelik/{olayAbonelikNo}", status_code=204)
def delete(olayAbonelikNo: str, h=Depends(headers), db: Session = Depends(get_db)):
    db.delete(_get(db, olayAbonelikNo, h))
    db.commit()
    return Response(status_code=204)


@router.get("/olay-abonelik/{olayAbonelikNo}/iletilemeyen-olaylar")
def undelivered(olayAbonelikNo: str, response: Response, syfKytSayi: int = Query(50, ge=1, le=100),
                syfNo: int = Query(1, ge=1), h=Depends(headers), db: Session = Depends(get_db)):
    _get(db, olayAbonelikNo, h)
    items = db.scalars(select(Event).where(Event.olay_abonelik_no == olayAbonelikNo, Event.status == "iletilemedi")
                       .order_by(Event.olay_zamani)).all()
    response.headers["X-Total-Count"] = str(len(items))
    return [events.payload(e) for e in items[(syfNo - 1) * syfKytSayi: syfNo * syfKytSayi]]
