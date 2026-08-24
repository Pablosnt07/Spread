"use client";

import { useEffect, useState } from "react";
import type { CompanyMarketActivity, InsiderCategory } from "@/lib/activity";
import type { CompanyProfile } from "@/lib/company-profile";
import type { CompanyFinancials } from "@/lib/financials";
import Link from "next/link";
import { companies, fundamentals, searchableAssets } from "@/lib/data";
import { CompanyLogo } from "./CompanyLogo";
import { IncomeStatementSection } from "./IncomeStatementSection";
import { MetricBars } from "./MetricBars";
import { PriceChart } from "./PriceChart";
import { RadarChart } from "./RadarChart";
import { ScoreGauge } from "./ScoreGauge";

const profiles = { Conservador: 76, Balanceado: 82, Crecimiento: 87 } as const;
const categoryLabels: Record<InsiderCategory, string> = {
  Purchase: "Compra",
  Sale: "Venta",
  Award: "Adjudicación",
  Exercise: "Ejercicio",
  Gift: "Donación",
  Other: "Movimiento",
};
const numberFormatter = new Intl.NumberFormat("es-AR", { maximumFractionDigits: 0 });
const moneyFormatter = new Intl.NumberFormat("es-AR", { style: "currency", currency: "USD", maximumFractionDigits: 0 });

function formatDate(value: string | null) {
  if (!value) return "—";
  const [year, month, day] = value.split("-");
  return `${day}/${month}/${year}`;
}

function formatDividend(value: number | null) {
  return value === null ? "—" : `USD ${value.toLocaleString("es-AR", { minimumFractionDigits: 2, maximumFractionDigits: 4 })}`;
}

function insiderOperation(category: InsiderCategory, rawType: string | null, direction: string | null) {
  if (category !== "Other") return categoryLabels[category];
  const raw = rawType?.trim() ?? "";
  const normalized = raw.toLowerCase();
  if ((normalized.includes("10b5") || normalized.includes("program") || normalized.includes("scheduled")) && (normalized.includes("sale") || direction === "D")) return "Venta prog.";
  if (normalized.includes("tax") || normalized.includes("withhold")) return "Retención fiscal";
  if (normalized.includes("convert")) return "Conversión";
  if (normalized.includes("gift")) return "Donación";
  if (raw && !["acquisition", "disposition"].includes(normalized)) return raw.length > 24 ? `${raw.slice(0, 22)}…` : raw;
  if (direction === "A" || normalized === "acquisition") return "Adquisición";
  if (direction === "D" || normalized === "disposition") return "Disposición";
  return "Movimiento";
}

export function CompanyAnalysis({ ticker }: { ticker: string }) {
  const [profile, setProfile] = useState<keyof typeof profiles>("Balanceado");
  const [activity, setActivity] = useState<CompanyMarketActivity | null>(null);
  const [companyProfile, setCompanyProfile] = useState<CompanyProfile | null>(null);
  const [financials, setFinancials] = useState<CompanyFinancials | null>(null);
  const [activityStatus, setActivityStatus] = useState<"loading" | "ready" | "empty" | "error">("loading");
  const [profileStatus, setProfileStatus] = useState<"loading" | "ready" | "not-found" | "error">("loading");
  const company = companies.find((candidate) => candidate.ticker === ticker);
  const searchAsset = searchableAssets.find((candidate) => candidate.ticker === ticker);
  const knownEtf = searchAsset?.assetType === "etf";
  const displayCompany = {
    ticker: companyProfile?.ticker ?? ticker,
    name: companyProfile?.companyName ?? searchAsset?.name ?? company?.name ?? ticker,
    exchange: companyProfile?.exchange ?? searchAsset?.exchange ?? company?.exchange ?? "Mercado no informado",
    sector: companyProfile?.sector ?? searchAsset?.sector ?? company?.sector ?? "Sector no informado",
    industry: companyProfile?.industry ?? searchAsset?.industry ?? company?.industry ?? "Industria no informada",
    logoUrl: companyProfile?.logoUrl ?? searchAsset?.logoUrl ?? company?.logoUrl,
  };
  const isEtf = knownEtf || companyProfile?.assetType === "ExchangeTradedFund";

  useEffect(() => {
    const controller = new AbortController();
    setActivityStatus(knownEtf ? "empty" : "loading");
    setActivity(null);
    setCompanyProfile(null);
    setFinancials(null);
    setProfileStatus("loading");

    const profileRequest = fetch(`/api/companies/${encodeURIComponent(ticker)}`, { signal: controller.signal })
      .then(async (response) => {
        if (response.status === 404) {
          setProfileStatus("not-found");
          return null;
        }
        if (!response.ok) throw new Error("profile-unavailable");
        return response.json() as Promise<CompanyProfile>;
      })
      .then((result) => {
        setCompanyProfile(result);
        if (result) setProfileStatus("ready");
      })
      .catch((error: unknown) => {
        if (error instanceof DOMException && error.name === "AbortError") return;
        setCompanyProfile(null);
        setProfileStatus("error");
      });

    if (knownEtf) {
      void profileRequest;
      return () => controller.abort();
    }

    const activityRequest = fetch(`/api/companies/${encodeURIComponent(ticker)}/activity`, { signal: controller.signal })
      .then(async (response) => {
        if (response.status === 404) return null;
        if (!response.ok) throw new Error("activity-unavailable");
        return response.json() as Promise<CompanyMarketActivity>;
      })
      .then((result) => {
        setActivity(result);
        setActivityStatus(result ? "ready" : "empty");
      })
      .catch((error: unknown) => {
        if (error instanceof DOMException && error.name === "AbortError") return;
        setActivityStatus("error");
      });

    const financialsRequest = fetch(`/api/companies/${encodeURIComponent(ticker)}/financials`, { signal: controller.signal })
      .then(async (response) => {
        if (!response.ok) return null;
        return response.json() as Promise<CompanyFinancials>;
      })
      .then((result) => setFinancials(result?.periods.length ? result : null))
      .catch((error: unknown) => {
        if (error instanceof DOMException && error.name === "AbortError") return;
        setFinancials(null);
      });

    void Promise.allSettled([activityRequest, profileRequest, financialsRequest]);

    return () => controller.abort();
  }, [knownEtf, ticker]);

  if (!company && profileStatus === "loading") {
    return <main className="analysis-shell"><section className="score-unavailable" role="status"><p>Perfil de empresa</p><h2>Consultando {ticker}…</h2><p>Validando el ticker y recuperando sus datos reales.</p></section></main>;
  }

  if (!company && profileStatus !== "ready") {
    return <main className="analysis-shell"><section className="unsupported-analysis"><p>Empresa no disponible</p><h2>No pudimos analizar {ticker}</h2><p>{profileStatus === "not-found" ? "FMP no devolvió un perfil para este ticker. Revisá el símbolo e intentá nuevamente." : "El proveedor de datos no está disponible temporalmente. No mostramos datos de otra empresa como reemplazo."}</p><Link href="/">← Volver al buscador</Link></section></main>;
  }

  if (isEtf) {
    return (
      <main className="analysis-shell etf-shell">
        <section className="company-header">
          <div className="company-identity"><CompanyLogo name={displayCompany.name} ticker={displayCompany.ticker} logoUrl={displayCompany.logoUrl ?? undefined} brandColor={searchAsset?.brandColor} /><div><p>{displayCompany.ticker} · {displayCompany.exchange}</p><h1>{displayCompany.name}</h1><span>{displayCompany.sector} · {displayCompany.industry}</span></div></div>
          <div className="asset-type-status"><span>Tipo de activo</span><strong>ETF detectado</strong></div>
        </section>
        <section className="unsupported-analysis" aria-labelledby="unsupported-title">
          <p>Modelo todavía no disponible</p>
          <h2 id="unsupported-title">Spread aún no analiza ETFs</h2>
          <p>{displayCompany.ticker} fue identificado correctamente como un fondo cotizado. El Company Score actual evalúa empresas operativas y no debe aplicarse a ETFs, que requieren métricas propias como composición, costos, tracking error, liquidez y concentración.</p>
          <div><span>Clasificación</span><strong>Exchange Traded Fund</strong><small>Detectado mediante el perfil normalizado de FMP</small></div>
          <Link href="/">← Buscar una empresa compatible</Link>
        </section>
      </main>
    );
  }

  const recentPurchase = activity?.insiderTransactions.find((transaction) => transaction.category === "Purchase");
  const latestDividend = activity?.dividends[0];

  return (
    <main className="analysis-shell">
      <section className="company-header">
        <div className="company-identity"><CompanyLogo name={displayCompany.name} ticker={displayCompany.ticker} logoUrl={displayCompany.logoUrl ?? undefined} brandColor={company?.brandColor ?? searchAsset?.brandColor} /><div><p>{displayCompany.ticker} · {displayCompany.exchange}</p><h1>{displayCompany.name}</h1><span>{displayCompany.sector} · {displayCompany.industry}</span></div></div>
        {company ? <div className="company-quote"><strong>{company.price}</strong><span className={company.change.startsWith("+") ? "positive" : "negative"}>{company.change} hoy</span><small>Cotización de demostración · 22 ago 2026</small></div> : <div className="company-quote"><strong>Datos reales</strong><small>Sin cotización simulada</small></div>}
      </section>

      <p className="company-summary">{company ? `${displayCompany.name} se analiza mediante seis dimensiones fundamentales. La lectura separa calidad corporativa, crecimiento, rentabilidad, valuación, salud financiera y riesgo sin convertir el resultado en una recomendación.` : `${displayCompany.name} fue identificada con datos reales de FMP. Sus estados financieros y actividad corporativa se muestran sin asignarle un score hasta que el motor cubra las seis dimensiones.`}</p>

      {company ? <section className="analysis-grid">
        <div className="analysis-chart"><PriceChart ticker={ticker} /><div className="fundamentals-strip">{fundamentals.map(([label, value, period]) => <div key={label}><span>{label}</span><strong>{value}</strong><small>{period}</small></div>)}</div></div>
        <aside className="score-panel">
          <ScoreGauge score={company.score} />
          <div className="confidence-row"><span>Confianza de datos</span><strong>{company.confidence}%</strong><i><b style={{ width: `${company.confidence}%` }} /></i><small>Cobertura alta · Modelo v0.1.0</small></div>
          <div className="profile-match"><div><span>Profile Match</span><strong>{profiles[profile]}%</strong></div><label htmlFor="profile">Perfil de inversión</label><select id="profile" value={profile} onChange={(event) => setProfile(event.target.value as keyof typeof profiles)}>{Object.keys(profiles).map((name) => <option key={name}>{name}</option>)}</select><p>Compatibilidad informativa; no constituye una recomendación.</p></div>
        </aside>
      </section> : <section className="score-unavailable" role="status"><p>Company Score</p><h2>Score todavía no calculado</h2><p>Spread encontró la empresa y muestra debajo sus estados financieros, dividendos y actividad de insiders cuando están disponibles. El score no se reemplaza por el de otra compañía ni se inventan métricas.</p><small>Se incorporará cuando el motor pueda calcular las seis dimensiones con cobertura suficiente.</small></section>}

      {company ? <><section className="signature-section">
        <div className="signature-copy"><p>Spread Signature</p><h2>Rentabilidad fuerte, valuación exigente.</h2><p>La forma muestra el equilibrio entre seis dimensiones. Los valores altos amplían el perfil; no representan una predicción de precio.</p></div>
        <RadarChart values={Object.values(company.metrics)} />
        <MetricBars />
      </section>

      <section className="evidence-section">
        <div className="evidence positive-evidence"><div><span aria-hidden="true">↗</span><h2>Aspectos positivos</h2></div><ul>{recentPurchase ? <li><strong>Compra insider reciente</strong><span>{recentPurchase.reportingName} declaró una compra de {recentPurchase.securitiesTransacted ? numberFormatter.format(recentPurchase.securitiesTransacted) : "acciones no detalladas"}. Se muestra como evidencia, fuera del Company Score.</span></li> : null}<li><strong>Rentabilidad sostenida</strong><span>ROIC de 28,7%, por encima de la mediana de sus comparables.</span></li><li><strong>Generación de caja</strong><span>USD 101,5B de FCF durante los últimos doce meses.</span></li><li><strong>Calidad operativa</strong><span>Márgenes estables y recurrencia creciente de servicios.</span></li></ul></div>
        <div className="evidence negative-evidence"><div><span aria-hidden="true">↘</span><h2>Aspectos negativos</h2></div><ul><li><strong>Valuación exigente</strong><span>P/E de 29,4×, superior a parte de sus comparables maduros.</span></li><li><strong>Concentración de ingresos</strong><span>El negocio principal conserva una participación material en los ingresos.</span></li><li><strong>Crecimiento moderado</strong><span>La escala puede limitar la velocidad de expansión de ingresos.</span></li></ul></div>
      </section></> : null}

      <IncomeStatementSection financials={financials} />

      <section className="dividend-section" aria-labelledby="dividend-title">
        <div className="section-heading"><div><p>Retorno al accionista</p><h2 id="dividend-title">Dividendos</h2></div><p>Pagos históricos informados por FMP. Fechas y montos se presentan como datos corporativos, no como proyección.</p></div>
        {activityStatus === "loading" ? <ActivityMessage title="Consultando dividendos" detail="Recuperando el historial más reciente de FMP…" /> : null}
        {activityStatus === "error" || (activityStatus === "ready" && activity && !activity.dividendDataAvailable) ? <ActivityMessage title="Datos no disponibles" detail="No pudimos consultar dividendos en FMP en este momento. El análisis principal sigue disponible." /> : null}
        {activityStatus === "empty" || (activityStatus === "ready" && activity?.dividendDataAvailable && activity.dividends.length === 0) ? <ActivityMessage title="Sin dividendos recientes" detail="FMP no devolvió eventos de dividendos para este ticker." /> : null}
        {activityStatus === "ready" && latestDividend ? <>
          <div className="dividend-summary"><div><span>Último dividendo ajustado</span><strong>{formatDividend(latestDividend.adjustedDividend ?? latestDividend.dividend)}</strong></div><div><span>Frecuencia</span><strong>{latestDividend.frequency ?? "—"}</strong></div><div><span>Fecha ex-dividendo</span><strong>{formatDate(latestDividend.exDividendDate)}</strong></div><div><span>Fecha de pago</span><strong>{formatDate(latestDividend.paymentDate)}</strong></div></div>
          <div className="dividend-history" role="table" aria-label="Historial reciente de dividendos">
            <div className="dividend-row dividend-head" role="row"><span>Ex-dividendo</span><span>Declaración</span><span>Registro</span><span>Pago</span><span>Dividendo</span></div>
            {activity.dividends.slice(0, 6).map((dividend) => <div className="dividend-row" role="row" key={`${dividend.exDividendDate}-${dividend.paymentDate ?? "pending"}`}><span data-label="Ex-dividendo">{formatDate(dividend.exDividendDate)}</span><span data-label="Declaración">{formatDate(dividend.declarationDate)}</span><span data-label="Registro">{formatDate(dividend.recordDate)}</span><span data-label="Pago">{formatDate(dividend.paymentDate)}</span><strong data-label="Dividendo">{formatDividend(dividend.adjustedDividend ?? dividend.dividend)}</strong></div>)}
          </div>
        </> : null}
      </section>

      <section className="insider-section" aria-labelledby="insider-title">
        <div className="section-heading"><div><p>Actividad corporativa</p><h2 id="insider-title">Operaciones de insiders</h2></div><p>Últimas declaraciones de directivos y propietarios. Compras, ventas, adjudicaciones, ejercicios y donaciones permanecen separadas del Company Score.</p></div>
        {activityStatus === "loading" ? <ActivityMessage title="Consultando insiders" detail="Clasificando las últimas declaraciones disponibles…" /> : null}
        {activityStatus === "error" || (activityStatus === "ready" && activity && !activity.insiderDataAvailable) ? <ActivityMessage title="Datos no disponibles" detail="La fuente externa no respondió; no mostramos operaciones simuladas." /> : null}
        {activityStatus === "empty" || (activityStatus === "ready" && activity?.insiderDataAvailable && activity.insiderTransactions.length === 0) ? <ActivityMessage title="Sin operaciones recientes" detail="Las fuentes configuradas no devolvieron movimientos para este ticker dentro del período consultado." /> : null}
        {activityStatus === "ready" && activity && activity.insiderTransactions.length > 0 ? <div className="insider-table" role="table" aria-label="Operaciones recientes de insiders">
          <div className="insider-row insider-head" role="row"><span>Fecha</span><span>Insider</span><span>Operación</span><span>Acciones</span><span>Valor</span><span>Fuente</span></div>
          {activity.insiderTransactions.map((transaction, index) => <div className="insider-row" role="row" key={`${transaction.filingDate}-${transaction.reportingName}-${index}`}><span data-label="Fecha">{formatDate(transaction.transactionDate ?? transaction.filingDate)}</span><span className="insider-person" data-label="Insider"><strong>{transaction.reportingName}</strong><small>{transaction.ownerType ?? "Rol no informado"}</small></span><span data-label="Operación"><b className={`activity-tag ${transaction.category.toLowerCase()}`}>{insiderOperation(transaction.category, transaction.transactionType, transaction.acquisitionOrDisposition)}</b><small>{transaction.transactionType ?? "Tipo no informado"}</small></span><span data-label="Acciones">{transaction.securitiesTransacted === null ? "—" : numberFormatter.format(transaction.securitiesTransacted)}</span><span data-label="Valor">{transaction.transactionValue === null ? "—" : moneyFormatter.format(transaction.transactionValue)}</span><span data-label="Fuente">{transaction.filingUrl ? <a href={transaction.filingUrl} target="_blank" rel="noreferrer" aria-label={`Ver filing de ${transaction.reportingName}`}>SEC ↗</a> : transaction.source}</span></div>)}
        </div> : null}
      </section>

      {company ? <section className="raw-data"><div className="section-heading"><div><p>Datos puros</p><h2>Fundamentales principales</h2></div><p>USD · Últimos doce meses, salvo indicación.</p></div><div className="raw-grid">{fundamentals.map(([label, value, period]) => <div key={label}><span>{label}</span><strong>{value}</strong><small>{period}</small></div>)}</div></section> : null}
    </main>
  );
}

function ActivityMessage({ title, detail }: { title: string; detail: string }) {
  return <div className="activity-message" role="status"><span aria-hidden="true">↗</span><div><strong>{title}</strong><p>{detail}</p></div></div>;
}
