"use client";

import { useEffect, useState } from "react";
import type { CompanyMarketActivity, InsiderCategory } from "@/lib/activity";
import type { CompanyProfile } from "@/lib/company-profile";
import type { CompanyFinancials } from "@/lib/financials";
import { companies, fundamentals } from "@/lib/data";
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
  Other: "Otra",
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

export function CompanyAnalysis({ ticker }: { ticker: string }) {
  const [profile, setProfile] = useState<keyof typeof profiles>("Balanceado");
  const [activity, setActivity] = useState<CompanyMarketActivity | null>(null);
  const [companyProfile, setCompanyProfile] = useState<CompanyProfile | null>(null);
  const [financials, setFinancials] = useState<CompanyFinancials | null>(null);
  const [activityStatus, setActivityStatus] = useState<"loading" | "ready" | "empty" | "error">("loading");
  const company = companies.find((candidate) => candidate.ticker === ticker) ?? companies[0];
  const displayCompany = {
    ticker: companyProfile?.ticker ?? ticker,
    name: companyProfile?.companyName ?? company.name,
    exchange: companyProfile?.exchange ?? company.exchange,
    sector: companyProfile?.sector ?? company.sector,
    industry: companyProfile?.industry ?? company.industry,
    logoUrl: companyProfile?.logoUrl ?? company.logoUrl,
  };

  useEffect(() => {
    const controller = new AbortController();
    setActivityStatus("loading");
    setActivity(null);
    setCompanyProfile(null);
    setFinancials(null);

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

    const profileRequest = fetch(`/api/companies/${encodeURIComponent(ticker)}`, { signal: controller.signal })
      .then(async (response) => {
        if (!response.ok) return null;
        return response.json() as Promise<CompanyProfile>;
      })
      .then((result) => setCompanyProfile(result))
      .catch((error: unknown) => {
        if (error instanceof DOMException && error.name === "AbortError") return;
        setCompanyProfile(null);
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
  }, [ticker]);

  const recentPurchase = activity?.insiderTransactions.find((transaction) => transaction.category === "Purchase");
  const latestDividend = activity?.dividends[0];

  return (
    <main className="analysis-shell">
      <section className="company-header">
        <div className="company-identity"><CompanyLogo name={displayCompany.name} ticker={displayCompany.ticker} logoUrl={displayCompany.logoUrl ?? undefined} brandColor={company.brandColor} /><div><p>{displayCompany.ticker} · {displayCompany.exchange}</p><h1>{displayCompany.name}</h1><span>{displayCompany.sector} · {displayCompany.industry}</span></div></div>
        <div className="company-quote"><strong>{company.price}</strong><span className={company.change.startsWith("+") ? "positive" : "negative"}>{company.change} hoy</span><small>Cotización de demostración · 22 ago 2026</small></div>
      </section>

      <p className="company-summary">{displayCompany.name} se analiza mediante seis dimensiones fundamentales. La lectura separa calidad corporativa, crecimiento, rentabilidad, valuación, salud financiera y riesgo sin convertir el resultado en una recomendación.</p>

      <section className="analysis-grid">
        <div className="analysis-chart"><PriceChart /><div className="fundamentals-strip">{fundamentals.map(([label, value, period]) => <div key={label}><span>{label}</span><strong>{value}</strong><small>{period}</small></div>)}</div></div>
        <aside className="score-panel">
          <ScoreGauge score={company.score} />
          <div className="confidence-row"><span>Confianza de datos</span><strong>{company.confidence}%</strong><i><b style={{ width: `${company.confidence}%` }} /></i><small>Cobertura alta · Modelo v0.1.0</small></div>
          <div className="profile-match"><div><span>Profile Match</span><strong>{profiles[profile]}%</strong></div><label htmlFor="profile">Perfil de inversión</label><select id="profile" value={profile} onChange={(event) => setProfile(event.target.value as keyof typeof profiles)}>{Object.keys(profiles).map((name) => <option key={name}>{name}</option>)}</select><p>Compatibilidad informativa; no constituye una recomendación.</p></div>
        </aside>
      </section>

      <section className="signature-section">
        <div className="signature-copy"><p>Spread Signature</p><h2>Rentabilidad fuerte, valuación exigente.</h2><p>La forma muestra el equilibrio entre seis dimensiones. Los valores altos amplían el perfil; no representan una predicción de precio.</p></div>
        <RadarChart values={Object.values(company.metrics)} />
        <MetricBars />
      </section>

      <section className="evidence-section">
        <div className="evidence positive-evidence"><div><span aria-hidden="true">↗</span><h2>Aspectos positivos</h2></div><ul>{recentPurchase ? <li><strong>Compra insider reciente</strong><span>{recentPurchase.reportingName} declaró una compra de {recentPurchase.securitiesTransacted ? numberFormatter.format(recentPurchase.securitiesTransacted) : "acciones no detalladas"}. Se muestra como evidencia, fuera del Company Score.</span></li> : null}<li><strong>Rentabilidad sostenida</strong><span>ROIC de 28,7%, por encima de la mediana de sus comparables.</span></li><li><strong>Generación de caja</strong><span>USD 101,5B de FCF durante los últimos doce meses.</span></li><li><strong>Calidad operativa</strong><span>Márgenes estables y recurrencia creciente de servicios.</span></li></ul></div>
        <div className="evidence negative-evidence"><div><span aria-hidden="true">↘</span><h2>Aspectos negativos</h2></div><ul><li><strong>Valuación exigente</strong><span>P/E de 29,4×, superior a parte de sus comparables maduros.</span></li><li><strong>Concentración de ingresos</strong><span>El negocio principal conserva una participación material en los ingresos.</span></li><li><strong>Crecimiento moderado</strong><span>La escala puede limitar la velocidad de expansión de ingresos.</span></li></ul></div>
      </section>

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
        {activityStatus === "empty" || (activityStatus === "ready" && activity?.insiderDataAvailable && activity.insiderTransactions.length === 0) ? <ActivityMessage title="Sin operaciones recientes" detail="Este ticker no aparece entre las últimas 100 declaraciones globales accesibles en el plan actual de FMP." /> : null}
        {activityStatus === "ready" && activity && activity.insiderTransactions.length > 0 ? <div className="insider-table" role="table" aria-label="Operaciones recientes de insiders">
          <div className="insider-row insider-head" role="row"><span>Fecha</span><span>Insider</span><span>Operación</span><span>Acciones</span><span>Valor</span><span>Fuente</span></div>
          {activity.insiderTransactions.map((transaction, index) => <div className="insider-row" role="row" key={`${transaction.filingDate}-${transaction.reportingName}-${index}`}><span data-label="Fecha">{formatDate(transaction.transactionDate ?? transaction.filingDate)}</span><span className="insider-person" data-label="Insider"><strong>{transaction.reportingName}</strong><small>{transaction.ownerType ?? "Rol no informado"}</small></span><span data-label="Operación"><b className={`activity-tag ${transaction.category.toLowerCase()}`}>{categoryLabels[transaction.category]}</b><small>{transaction.transactionType ?? "Tipo no informado"}</small></span><span data-label="Acciones">{transaction.securitiesTransacted === null ? "—" : numberFormatter.format(transaction.securitiesTransacted)}</span><span data-label="Valor">{transaction.transactionValue === null ? "—" : moneyFormatter.format(transaction.transactionValue)}</span><span data-label="Fuente">{transaction.filingUrl ? <a href={transaction.filingUrl} target="_blank" rel="noreferrer" aria-label={`Ver filing de ${transaction.reportingName}`}>SEC ↗</a> : "—"}</span></div>)}
        </div> : null}
      </section>

      <section className="raw-data"><div className="section-heading"><div><p>Datos puros</p><h2>Fundamentales principales</h2></div><p>USD · Últimos doce meses, salvo indicación.</p></div><div className="raw-grid">{fundamentals.map(([label, value, period]) => <div key={label}><span>{label}</span><strong>{value}</strong><small>{period}</small></div>)}</div></section>
    </main>
  );
}

function ActivityMessage({ title, detail }: { title: string; detail: string }) {
  return <div className="activity-message" role="status"><span aria-hidden="true">↗</span><div><strong>{title}</strong><p>{detail}</p></div></div>;
}
