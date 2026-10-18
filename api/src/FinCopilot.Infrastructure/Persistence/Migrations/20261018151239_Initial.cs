using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace FinCopilot.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "banks",
                columns: table => new
                {
                    hhs_kod = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    logo_url = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    api_base_url = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    supports_decoupled = table.Column<bool>(type: "bit", nullable: false),
                    is_active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_banks", x => x.hhs_kod);
                });

            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    slug = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    name_tr = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    parent_id = table.Column<int>(type: "int", nullable: true),
                    icon = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    is_income = table.Column<bool>(type: "bit", nullable: false),
                    sort_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_categories", x => x.id);
                    table.ForeignKey(
                        name: "fk_categories_categories_parent_id",
                        column: x => x.parent_id,
                        principalTable: "categories",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "data_protection_keys",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    friendly_name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    xml = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_protection_keys", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "event_inbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    olay_no = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    olay_tipi = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    kaynak_tipi = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    kaynak_no = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    payload = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: false),
                    received_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_event_inbox", x => x.id);
                    table.CheckConstraint("ck_event_inbox_payload_json", "ISJSON([payload]) = 1");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    phone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    tckn_enc = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    tckn_hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    full_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    pin_hash = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "merchants",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    canonical_name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    display_name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    logo_url = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    default_category_id = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_merchants", x => x.id);
                    table.ForeignKey(
                        name: "fk_merchants_categories_default_category_id",
                        column: x => x.default_category_id,
                        principalTable: "categories",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "alerts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    severity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    payload = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: false),
                    dedupe_key = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    read_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_alerts", x => x.id);
                    table.CheckConstraint("ck_alerts_payload_json", "ISJSON([payload]) = 1");
                    table.ForeignKey(
                        name: "fk_alerts_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "consents",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    hhs_kod = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    riza_no = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    riza_drm = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    riza_ipt_dty_kod = table.Column<string>(type: "nvarchar(2)", maxLength: 2, nullable: true),
                    izin_turleri = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    erisim_izni_son_trh = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    hesap_isl_bsl_zmn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    hesap_isl_bts_zmn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    gkd_yontem = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    drm_kod_hash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consents", x => x.id);
                    table.CheckConstraint("ck_consents_izin_turleri_json", "ISJSON([izin_turleri]) = 1");
                    table.CheckConstraint("ck_consents_riza_drm", "[riza_drm] IN ('B','Y','K','S','I')");
                    table.ForeignKey(
                        name: "fk_consents_banks_hhs_kod",
                        column: x => x.hhs_kod,
                        principalTable: "banks",
                        principalColumn: "hhs_kod");
                    table.ForeignKey(
                        name: "fk_consents_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "goals",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    target_amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    target_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_goals", x => x.id);
                    table.ForeignKey(
                        name: "fk_goals_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_devices",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    device_id = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    platform = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    fcm_token = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    last_seen_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_devices", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_devices_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_settings",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    payday_day = table.Column<int>(type: "int", nullable: true),
                    kmh_rate = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    card_interest_rate = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    late_interest_rate = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    timezone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    notify_daily_brief = table.Column<bool>(type: "bit", nullable: false),
                    notify_alerts = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_settings", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_user_settings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "merchant_aliases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    pattern = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    pattern_type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    source = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_merchant_aliases", x => x.id);
                    table.ForeignKey(
                        name: "fk_merchant_aliases_merchants_merchant_id",
                        column: x => x.merchant_id,
                        principalTable: "merchants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recurring_items",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    type = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    counterparty_key = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    merchant_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    expected_amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    amount_tolerance = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    period_days = table.Column<int>(type: "int", nullable: false),
                    day_of_month = table.Column<int>(type: "int", nullable: true),
                    last_seen_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    next_expected_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    source = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recurring_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_recurring_items_merchants_merchant_id",
                        column: x => x.merchant_id,
                        principalTable: "merchants",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_recurring_items_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "savings_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    amount_saved = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    alert_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    evidence = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    detected_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_savings_events", x => x.id);
                    table.CheckConstraint("ck_savings_events_evidence_json", "[evidence] IS NULL OR ISJSON([evidence]) = 1");
                    table.ForeignKey(
                        name: "fk_savings_events_alerts_alert_id",
                        column: x => x.alert_id,
                        principalTable: "alerts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_savings_events_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    consent_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    hhs_kod = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    hsp_ref = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    hsp_no_enc = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    hsp_no_masked = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    hsp_shb = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    sube_adi = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    kisa_ad = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    urun_adi = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    para_birimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    hsp_tur = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    hsp_tip = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    hsp_drm = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    acilis_trh = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_accounts", x => x.id);
                    table.ForeignKey(
                        name: "fk_accounts_consents_consent_id",
                        column: x => x.consent_id,
                        principalTable: "consents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_accounts_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "cards",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    user_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    consent_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    hhs_kod = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    kart_ref = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    son_no = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    kart_tipi = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: false),
                    alt_kart_tipi = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    kart_formu = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    kart_turu = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    kart_statu = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    kart_sema = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    urun_adi = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    is_active = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cards", x => x.id);
                    table.ForeignKey(
                        name: "fk_cards_consents_consent_id",
                        column: x => x.consent_id,
                        principalTable: "consents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_cards_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "consent_tokens",
                columns: table => new
                {
                    consent_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    access_token_enc = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    access_expires_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    refresh_token_enc = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    refresh_expires_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_consent_tokens", x => x.consent_id);
                    table.ForeignKey(
                        name: "fk_consent_tokens_consents_consent_id",
                        column: x => x.consent_id,
                        principalTable: "consents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "sync_jobs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    consent_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    resource = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    psu_initiated = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    window_start = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    window_end = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    http_status = table.Column<int>(type: "int", nullable: true),
                    error_code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_sync_jobs", x => x.id);
                    table.ForeignKey(
                        name: "fk_sync_jobs_consents_consent_id",
                        column: x => x.consent_id,
                        principalTable: "consents",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "balance_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    account_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    bky_ttr = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    blk_ttr = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    para_birimi = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    kul_krd_ttr = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    krd_dhl_gstr = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: true),
                    bky_zmn = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    source = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_balance_snapshots", x => x.id);
                    table.ForeignKey(
                        name: "fk_balance_snapshots_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    account_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    isl_no = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ref_no = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    direction = table.Column<string>(type: "nvarchar(1)", maxLength: 1, nullable: false),
                    isl_tur = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    isl_amc = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    kanal = table.Column<string>(type: "nvarchar(4)", maxLength: 4, nullable: true),
                    description_raw = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    ohk_msj = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    counterparty_name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    counterparty_iban_masked = table.Column<string>(type: "nvarchar(34)", maxLength: 34, nullable: true),
                    occurred_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    merchant_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    category_id = table.Column<int>(type: "int", nullable: true),
                    category_source = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    category_confidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    recurring_item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_transactions", x => x.id);
                    table.ForeignKey(
                        name: "fk_transactions_accounts_account_id",
                        column: x => x.account_id,
                        principalTable: "accounts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_transactions_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_transactions_merchants_merchant_id",
                        column: x => x.merchant_id,
                        principalTable: "merchants",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_transactions_recurring_items_recurring_item_id",
                        column: x => x.recurring_item_id,
                        principalTable: "recurring_items",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "card_statements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    card_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    statement_date = table.Column<DateOnly>(type: "date", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    period_debt = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    min_payment = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    credit_limit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    used_limit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    cash_limit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    used_cash_limit = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: true),
                    currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    fetched_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_card_statements", x => x.id);
                    table.ForeignKey(
                        name: "fk_card_statements_cards_card_id",
                        column: x => x.card_id,
                        principalTable: "cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "card_transactions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    card_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    islem_no = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    installment_count = table.Column<int>(type: "int", nullable: false),
                    isl_turu = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    merchant_raw = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    description_raw = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    fx_info = table.Column<string>(type: "nvarchar(max)", maxLength: 256, nullable: true),
                    merchant_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    category_id = table.Column<int>(type: "int", nullable: true),
                    category_source = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    category_confidence = table.Column<decimal>(type: "decimal(5,4)", precision: 5, scale: 4, nullable: true),
                    recurring_item_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_card_transactions", x => x.id);
                    table.CheckConstraint("ck_card_transactions_fx_info_json", "[fx_info] IS NULL OR ISJSON([fx_info]) = 1");
                    table.ForeignKey(
                        name: "fk_card_transactions_cards_card_id",
                        column: x => x.card_id,
                        principalTable: "cards",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_card_transactions_categories_category_id",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_card_transactions_merchants_merchant_id",
                        column: x => x.merchant_id,
                        principalTable: "merchants",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "fk_card_transactions_recurring_items_recurring_item_id",
                        column: x => x.recurring_item_id,
                        principalTable: "recurring_items",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "installments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    card_transaction_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    card_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    installment_no = table.Column<int>(type: "int", nullable: false),
                    total_installments = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(19,4)", precision: 19, scale: 4, nullable: false),
                    due_month = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_installments", x => x.id);
                    table.ForeignKey(
                        name: "fk_installments_card_transactions_card_transaction_id",
                        column: x => x.card_transaction_id,
                        principalTable: "card_transactions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_installments_cards_card_id",
                        column: x => x.card_id,
                        principalTable: "cards",
                        principalColumn: "id");
                });

            migrationBuilder.InsertData(
                table: "categories",
                columns: new[] { "id", "icon", "is_income", "name_tr", "parent_id", "slug", "sort_order" },
                values: new object[,]
                {
                    { 1, null, false, "Market", null, "market", 1 },
                    { 2, null, false, "Yeme-İçme", null, "yeme-icme", 2 },
                    { 3, null, false, "Ulaşım", null, "ulasim", 3 },
                    { 4, null, false, "Fatura", null, "fatura", 4 },
                    { 5, null, false, "Kira", null, "kira", 5 },
                    { 6, null, false, "Abonelik", null, "abonelik", 6 },
                    { 7, null, false, "Sağlık", null, "saglik", 7 },
                    { 8, null, false, "Eğitim", null, "egitim", 8 },
                    { 9, null, false, "Eğlence", null, "eglence", 9 },
                    { 10, null, false, "Giyim", null, "giyim", 10 },
                    { 11, null, true, "Maaş", null, "maas", 11 },
                    { 12, null, false, "Transfer", null, "transfer", 12 },
                    { 13, null, false, "Banka Masrafı", null, "banka-masrafi", 13 },
                    { 14, null, false, "Diğer", null, "diger", 14 }
                });

            migrationBuilder.CreateIndex(
                name: "ix_accounts_consent_id_hsp_ref",
                table: "accounts",
                columns: new[] { "consent_id", "hsp_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_accounts_user_id",
                table: "accounts",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_alerts_user_id_dedupe_key",
                table: "alerts",
                columns: new[] { "user_id", "dedupe_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_balance_snapshots_account_id_bky_zmn",
                table: "balance_snapshots",
                columns: new[] { "account_id", "bky_zmn" });

            migrationBuilder.CreateIndex(
                name: "ix_card_statements_card_id_statement_date",
                table: "card_statements",
                columns: new[] { "card_id", "statement_date" });

            migrationBuilder.CreateIndex(
                name: "ix_card_transactions_card_id_islem_no",
                table: "card_transactions",
                columns: new[] { "card_id", "islem_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_card_transactions_card_id_occurred_at",
                table: "card_transactions",
                columns: new[] { "card_id", "occurred_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_card_transactions_category_id",
                table: "card_transactions",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "ix_card_transactions_merchant_id",
                table: "card_transactions",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "ix_card_transactions_recurring_item_id",
                table: "card_transactions",
                column: "recurring_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_cards_consent_id_kart_ref",
                table: "cards",
                columns: new[] { "consent_id", "kart_ref" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_cards_user_id",
                table: "cards",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_categories_parent_id",
                table: "categories",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "ix_categories_slug",
                table: "categories",
                column: "slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_consents_erisim_izni_son_trh",
                table: "consents",
                column: "erisim_izni_son_trh");

            migrationBuilder.CreateIndex(
                name: "ix_consents_hhs_kod",
                table: "consents",
                column: "hhs_kod");

            migrationBuilder.CreateIndex(
                name: "ix_consents_riza_no",
                table: "consents",
                column: "riza_no",
                unique: true,
                filter: "[riza_no] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ux_consents_user_bank_active",
                table: "consents",
                columns: new[] { "user_id", "hhs_kod" },
                unique: true,
                filter: "[riza_drm] IN ('B','Y','K')");

            migrationBuilder.CreateIndex(
                name: "ix_event_inbox_olay_no",
                table: "event_inbox",
                column: "olay_no",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_goals_user_id",
                table: "goals",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_installments_card_id_due_month",
                table: "installments",
                columns: new[] { "card_id", "due_month" });

            migrationBuilder.CreateIndex(
                name: "ix_installments_card_transaction_id",
                table: "installments",
                column: "card_transaction_id");

            migrationBuilder.CreateIndex(
                name: "ix_merchant_aliases_merchant_id",
                table: "merchant_aliases",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "ix_merchants_canonical_name",
                table: "merchants",
                column: "canonical_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_merchants_default_category_id",
                table: "merchants",
                column: "default_category_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_items_merchant_id",
                table: "recurring_items",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "ix_recurring_items_user_id_status",
                table: "recurring_items",
                columns: new[] { "user_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_savings_events_alert_id",
                table: "savings_events",
                column: "alert_id");

            migrationBuilder.CreateIndex(
                name: "ix_savings_events_user_id",
                table: "savings_events",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_sync_jobs_consent_id_started_at",
                table: "sync_jobs",
                columns: new[] { "consent_id", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_account_id_isl_no",
                table: "transactions",
                columns: new[] { "account_id", "isl_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_transactions_account_id_occurred_at",
                table: "transactions",
                columns: new[] { "account_id", "occurred_at" },
                descending: new[] { false, true })
                .Annotation("SqlServer:Include", new[] { "amount", "category_id" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_category_id_occurred_at",
                table: "transactions",
                columns: new[] { "category_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_transactions_merchant_id",
                table: "transactions",
                column: "merchant_id");

            migrationBuilder.CreateIndex(
                name: "ix_transactions_recurring_item_id",
                table: "transactions",
                column: "recurring_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_devices_user_id_device_id",
                table: "user_devices",
                columns: new[] { "user_id", "device_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_phone",
                table: "users",
                column: "phone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_tckn_hash",
                table: "users",
                column: "tckn_hash",
                unique: true,
                filter: "[tckn_hash] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "balance_snapshots");

            migrationBuilder.DropTable(
                name: "card_statements");

            migrationBuilder.DropTable(
                name: "consent_tokens");

            migrationBuilder.DropTable(
                name: "data_protection_keys");

            migrationBuilder.DropTable(
                name: "event_inbox");

            migrationBuilder.DropTable(
                name: "goals");

            migrationBuilder.DropTable(
                name: "installments");

            migrationBuilder.DropTable(
                name: "merchant_aliases");

            migrationBuilder.DropTable(
                name: "savings_events");

            migrationBuilder.DropTable(
                name: "sync_jobs");

            migrationBuilder.DropTable(
                name: "transactions");

            migrationBuilder.DropTable(
                name: "user_devices");

            migrationBuilder.DropTable(
                name: "user_settings");

            migrationBuilder.DropTable(
                name: "card_transactions");

            migrationBuilder.DropTable(
                name: "alerts");

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropTable(
                name: "cards");

            migrationBuilder.DropTable(
                name: "recurring_items");

            migrationBuilder.DropTable(
                name: "consents");

            migrationBuilder.DropTable(
                name: "merchants");

            migrationBuilder.DropTable(
                name: "banks");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "categories");
        }
    }
}
