"""Hesap Bilgisi Hizmeti veri uçları: hesaplar, bakiye, işlemler, kartlar, kart detay, kart hareketleri.

Kurallar:
- X-Access-Token zorunlu, rıza durumu K olmalı.
- Sadece kullanıcının GKD'de seçtiği hesap ve kartlar görünür.
- İzin türleri: 01/02 hesap, 03 bakiye, 04/05 işlem, 07 kart, 08 kart detay, 09 kart hareketleri.
- PSU-Initiated=E (ÖHK tetikli): işlem sorgu aralığı en fazla 1 ay (bireysel).
- PSU-Initiated=H (sistem tetikli): aralık en fazla 24 saat, günde en fazla 4 başarılı sorgu (bireysel).
"""
from datetime import datetime, timedelta
from typing import Literal

from fastapi import APIRouter, Depends, Header, Query, Request, Response
from sqlalchemy import func, select
from sqlalchemy.orm import Session

from .. import config, errors
from .. import consent_service as svc
from ..config import TR_TZ
from ..db import QueryLog, get_db
from ..generator import store
from ..generator.personas import PERSONAS

router = APIRouter(tags=["Hesap Bilgisi"])


class Ctx:
    def __init__(self, consent, psu: str, request: Request, db: Session):
        self.c = consent
        self.psu = psu
        self.request = request
        self.db = db
        self.persona = PERSONAS.get(consent.kmlk_vrs)
        self.now = datetime.now(TR_TZ)

    def need(self, izin: str):
        if izin not in self.c.izin_tur:
            raise errors.permission_missing(izin)

    def sim(self):
        if self.persona is None:
            raise errors.resource_not_found("Kullanıcıya ait veri yok.")
        return store.dataset(self.c.kmlk_vrs)

    def accounts(self):
        sim = self.sim()
        return [store.account_view(self.c.kmlk_vrs, r, self.db) for r in self.c.secili_hesaplar if r in sim.accounts]

    def account(self, ref: str):
        if ref not in self.c.secili_hesaplar:
            raise errors.resource_not_found()
        self.sim()
        return store.account_view(self.c.kmlk_vrs, ref, self.db)

    def card_metas(self):
        by_ref = {c["ref"]: c for c in self.persona["cards"]} if self.persona else {}
        return [by_ref[r] for r in self.c.secili_kartlar if r in by_ref]

    def card_meta(self, ref: str):
        if ref not in self.c.secili_kartlar:
            raise errors.resource_not_found()
        return next(c for c in self.persona["cards"] if c["ref"] == ref)

    def credit_card(self, ref: str):
        self.card_meta(ref)
        sim = self.sim()
        if ref not in sim.cards:
            raise errors.resource_not_found("Bu kart için kredi kartı detayı bulunmuyor.")
        return sim.cards[ref]

    # ---- sistem tetikli kota
    def check_quota(self):
        if self.psu != "H":
            return
        day = self.now.date().isoformat()
        used = self.db.scalar(select(func.count()).select_from(QueryLog).where(
            QueryLog.riza_no == self.c.riza_no, QueryLog.resource == self.request.url.path, QueryLog.day == day))
        if used >= config.SYSTEM_DAILY_LIMIT.get(self.c.ohk_tur, 4):
            raise errors.exceeded_rate()

    def log_success(self):
        if self.psu == "H":
            self.db.add(QueryLog(riza_no=self.c.riza_no, resource=self.request.url.path,
                                 day=self.now.date().isoformat(), created_at=svc.now()))
            self.db.commit()

    # ---- tarih aralığı
    def window(self, bsl: datetime | None, bts: datetime | None) -> tuple[datetime, datetime]:
        if bsl is None or bts is None:
            raise errors.invalid_time("hesapIslemBslTrh ve hesapIslemBtsTrh zorunlu.")
        bsl, bts = svc.aware(bsl), svc.aware(bts)
        if bsl >= bts:
            raise errors.invalid_time("Başlangıç bitişten önce olmalı.")
        limit = config.SYSTEM_WINDOW if self.psu == "H" else config.PSU_WINDOW[self.c.ohk_tur]
        if bts - bsl > limit:
            raise errors.invalid_time(f"PSU-Initiated={self.psu} için sorgu aralığı en fazla {limit}.")
        c_bsl, c_bts = svc.aware(self.c.hesap_isl_bsl_zmn), svc.aware(self.c.hesap_isl_bts_zmn)
        if (c_bsl and bsl < c_bsl) or (c_bts and bts > c_bts):
            raise errors.invalid_time("Sorgu aralığı rızada izin verilen işlem aralığının dışında.")
        return bsl, bts


def ctx(
    request: Request,
    response: Response,
    x_request_id: str = Header(..., alias="X-Request-ID"),
    x_aspsp_code: str = Header(..., alias="X-ASPSP-Code"),
    x_tpp_code: str = Header(..., alias="X-TPP-Code"),
    x_access_token: str | None = Header(None, alias="X-Access-Token"),
    psu_initiated: Literal["E", "H"] = Header(..., alias="PSU-Initiated"),
    db: Session = Depends(get_db),
) -> Ctx:
    response.headers["X-Request-ID"] = x_request_id
    c = svc.require_access_token(db, x_access_token)
    if c.hhs_kod != x_aspsp_code:
        raise errors.invalid_aspsp()
    if c.yos_kod != x_tpp_code:
        raise errors.invalid_tpp()
    cx = Ctx(c, psu_initiated, request, db)
    cx.check_quota()
    return cx


def paginate(items: list, response: Response, size: int, page: int) -> list:
    total = len(items)
    pages = max(1, -(-total // size))
    response.headers["X-Total-Count"] = str(total)
    response.headers["X-Total-Pages"] = str(pages)
    return items[(page - 1) * size: page * size]


# ------------------------------------------------------------------ hesaplar
@router.get("/hesaplar")
def accounts(cx: Ctx = Depends(ctx)):
    cx.need("01")
    out = [store.map_account(a.meta, cx.persona, cx.c.riza_no, "02" in cx.c.izin_tur) for a in cx.accounts()]
    cx.log_success()
    return out


@router.get("/hesaplar/{hspRef}")
def account(hspRef: str, cx: Ctx = Depends(ctx)):
    cx.need("01")
    out = store.map_account(cx.account(hspRef).meta, cx.persona, cx.c.riza_no, "02" in cx.c.izin_tur)
    cx.log_success()
    return out


# ------------------------------------------------------------------ bakiye
@router.get("/bakiye")
def balances(cx: Ctx = Depends(ctx)):
    cx.need("03")
    out = [store.map_balance(a, cx.now) for a in cx.accounts()]
    cx.log_success()
    return out


@router.get("/hesaplar/{hspRef}/bakiye")
def balance(hspRef: str, cx: Ctx = Depends(ctx)):
    cx.need("03")
    out = store.map_balance(cx.account(hspRef), cx.now)
    cx.log_success()
    return out


# ------------------------------------------------------------------ işlemler
@router.get("/hesaplar/{hspRef}/islemler")
def transactions(
    hspRef: str,
    response: Response,
    hesapIslemBslTrh: datetime | None = Query(None),
    hesapIslemBtsTrh: datetime | None = Query(None),
    minIslTtr: float | None = Query(None, ge=0),
    mksIslTtr: float | None = Query(None, ge=0),
    brcAlc: Literal["B", "A"] | None = Query(None),
    syfKytSayi: int = Query(50, ge=1, le=config.MAX_PAGE_SIZE),
    syfNo: int = Query(1, ge=1),
    srlmKrtr: Literal["islGrckZaman", "islTtr"] = Query("islGrckZaman"),
    srlmYon: Literal["A", "Y"] = Query("Y", description="A: artan, Y: azalan"),
    cx: Ctx = Depends(ctx),
):
    cx.need("04")
    acc = cx.account(hspRef)
    bsl, bts = cx.window(hesapIslemBslTrh, hesapIslemBtsTrh)
    bts = min(bts, cx.now)
    items = [t for t in acc.txs if bsl <= t["islGrckZaman"] <= bts]
    if minIslTtr is not None:
        items = [t for t in items if t["islTtr"] >= minIslTtr]
    if mksIslTtr is not None:
        items = [t for t in items if t["islTtr"] <= mksIslTtr]
    if brcAlc:
        items = [t for t in items if t["brcAlc"] == brcAlc]
    items.sort(key=lambda t: t[srlmKrtr], reverse=(srlmYon == "Y"))
    page = paginate(items, response, syfKytSayi, syfNo)
    out = {"hspRef": hspRef, "isller": [store.map_tx(t, "05" in cx.c.izin_tur) for t in page]}
    cx.log_success()
    return out


# ------------------------------------------------------------------ kartlar
@router.get("/kartlar")
def cards(cx: Ctx = Depends(ctx)):
    cx.need("07")
    out = [store.map_card(m, cx.persona, cx.c.riza_no) for m in cx.card_metas()]
    cx.log_success()
    return out


@router.get("/kartlar/{kartRef}")
def card(kartRef: str, cx: Ctx = Depends(ctx)):
    cx.need("07")
    out = store.map_card(cx.card_meta(kartRef), cx.persona, cx.c.riza_no)
    cx.log_success()
    return out


@router.get("/kartlar/{kartRef}/kart-detay")
def card_detail(kartRef: str, cx: Ctx = Depends(ctx)):
    cx.need("08")
    out = store.map_card_detail(cx.credit_card(kartRef), cx.now)
    cx.log_success()
    return out


@router.get("/kartlar/{kartRef}/kart-hareketleri")
def card_transactions(
    kartRef: str,
    response: Response,
    hesapIslemBslTrh: datetime | None = Query(None),
    hesapIslemBtsTrh: datetime | None = Query(None),
    syfKytSayi: int = Query(50, ge=1, le=config.MAX_PAGE_SIZE),
    syfNo: int = Query(1, ge=1),
    srlmYon: Literal["A", "Y"] = Query("Y"),
    cx: Ctx = Depends(ctx),
):
    cx.need("09")
    card_state = cx.credit_card(kartRef)
    bsl, bts = cx.window(hesapIslemBslTrh, hesapIslemBtsTrh)
    bts = min(bts, cx.now)
    items = [m for m in card_state.movements if bsl <= m["islemTarihi"] <= bts]
    items.sort(key=lambda m: m["islemTarihi"], reverse=(srlmYon == "Y"))
    page = paginate(items, response, syfKytSayi, syfNo)
    out = {"kartRef": kartRef, "kartHareketleri": [store.map_card_tx(m) for m in page]}
    cx.log_success()
    return out
