"use client";

import { useEffect, useMemo, useState, type CSSProperties, type FormEvent } from "react";
import Link from "next/link";
import { companies, type Company } from "@/lib/data";
import { PriceChart, type ChartRange, type ChartSeries, type PricePoint } from "./PriceChart";
import { RadarChart } from "./RadarChart";
import { ScoreGauge } from "./ScoreGauge";

type Tab = "portfolio" | "watchlist" | "comparador";
type InvestorProfile = "Conservador" | "Moderado" | "Crecimiento";

type Holding = Pick<Company, "name" | "ticker" | "sector" | "score"> & {
  invested: number;
  day: string;
};

type AllocationSummary = {
  assetCount: number;
  totalInvested: number;
  positions: Array<{ ticker: string; investedAmount: number; allocationPercentage: number }>;
};

const initialHoldings: Holding[] = [
  { name: "Apple Inc.", ticker: "AAPL", sector: "Tecnología", invested: 420000, day: "+1,02%", score: 74 },
  { name: "Microsoft", ticker: "MSFT", sector: "Tecnología", invested: 350000, day: "+0,48%", score: 79 },
  { name: "MercadoLibre", ticker: "MELI", sector: "Consumo", invested: 180000, day: "+2,37%", score: 81 },
];

const allocationColors = ["var(--indigo)", "var(--mint)", "oklch(0.64 0.09 205)", "oklch(0.56 0.055 235)"];
const formatMoney = (value: number) => `USD ${value.toLocaleString("es-AR", { maximumFractionDigits: 0 })}`;
const formatPercent = (value: number) => `${value.toLocaleString("es-AR", { minimumFractionDigits: 1, maximumFractionDigits: 1 })}%`;
const initialTotal = initialHoldings.reduce((sum, position) => sum + position.invested, 0);
const initialAllocation: AllocationSummary = {
  assetCount: initialHoldings.length,
  totalInvested: initialTotal,
  positions: initialHoldings.map((position) => ({
    ticker: position.ticker,
    investedAmount: position.invested,
    allocationPercentage: position.invested / initialTotal * 100,
  })),
};

export function Workspace({ initialTab = "portfolio" }: { initialTab?: string }) {
  const normalized = (["portfolio", "watchlist", "comparador"].includes(initialTab) ? initialTab : "portfolio") as Tab;
  const [tab, setTab] = useState<Tab>(normalized);
  const [profile, setProfile] = useState<InvestorProfile>("Moderado");
  return (
    <main className="workspace-shell">
      <div className="workspace-title"><div><p>Tu espacio de análisis</p><h1>{tab === "portfolio" ? "Portfolio" : tab === "watchlist" ? "Watchlist" : "Comparador"}</h1></div><div className="profile-switch" aria-label="Perfil de inversión">{(["Conservador", "Moderado", "Crecimiento"] as InvestorProfile[]).map((item) => <button className={profile === item ? "selected" : ""} onClick={() => setProfile(item)} type="button" key={item}>{item}</button>)}</div></div>
      <div className="workspace-tabs" role="tablist">{(["portfolio", "watchlist", "comparador"] as Tab[]).map((item) => <button key={item} role="tab" aria-selected={tab === item} onClick={() => setTab(item)}>{item[0].toUpperCase() + item.slice(1)}</button>)}</div>
      {tab === "portfolio" && <Portfolio />}
      {tab === "watchlist" && <Watchlist />}
      {tab === "comparador" && <Comparator profile={profile} />}
    </main>
  );
}

function Portfolio() {
  const [positions, setPositions] = useState<Holding[]>(initialHoldings);
  const [adding, setAdding] = useState(false);
  const [selectedTicker, setSelectedTicker] = useState("NVDA");
  const [investment, setInvestment] = useState("");
  const [message, setMessage] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [allocationSummary, setAllocationSummary] = useState<AllocationSummary>(initialAllocation);
  const totalInvested = allocationSummary.totalInvested;
  const allocationByTicker = useMemo(() => new Map(allocationSummary.positions.map((position) => [position.ticker, position.allocationPercentage])), [allocationSummary]);
  const availableCompanies = useMemo(() => {
    const used = new Set(positions.map((position) => position.ticker));
    return companies.filter((company) => !used.has(company.ticker));
  }, [positions]);
  const sectorAllocations = useMemo(() => {
    const totals = new Map<string, number>();
    positions.forEach((position) => totals.set(position.sector, (totals.get(position.sector) ?? 0) + (allocationByTicker.get(position.ticker) ?? 0)));
    return Array.from(totals, ([sector, value], index) => ({
      sector,
      value,
      percent: value,
      color: allocationColors[index % allocationColors.length],
    }));
  }, [positions, allocationByTicker]);
  const assetAllocations = useMemo(() => positions.map((position, index) => ({
    ticker: position.ticker,
    name: position.name,
    percent: allocationByTicker.get(position.ticker) ?? 0,
    color: allocationColors[index % allocationColors.length],
  })), [positions, allocationByTicker]);
  const donutGradient = useMemo(() => {
    let cursor = 0;
    return assetAllocations.map((allocation) => {
      const start = cursor;
      cursor += allocation.percent;
      return `${allocation.color} ${start}% ${cursor}%`;
    }).join(", ");
  }, [assetAllocations]);

  const calculateAllocation = async (nextPositions: Holding[]) => {
    if (nextPositions.length === 0) return { assetCount: 0, totalInvested: 0, positions: [] } satisfies AllocationSummary;
    const response = await fetch("/api/portfolio-allocation", {
      method: "POST",
      headers: { "content-type": "application/json" },
      body: JSON.stringify({ baseCurrency: "USD", positions: nextPositions.map((position) => ({ ticker: position.ticker, investedAmount: position.invested })) }),
    });
    if (!response.ok) throw new Error("Allocation request failed");
    return await response.json() as AllocationSummary;
  };

  const removePosition = async (ticker: string) => {
    const nextPositions = positions.filter((position) => position.ticker !== ticker);
    setSubmitting(true); setMessage("Recalculando portfolio…");
    try {
      const summary = await calculateAllocation(nextPositions);
      setPositions(nextPositions); setAllocationSummary(summary); setMessage(`${ticker} fue eliminado del portfolio.`);
    } catch { setMessage("No pudimos actualizar el portfolio. Intentá nuevamente."); }
    finally { setSubmitting(false); }
  };

  const addPosition = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const amount = Number(investment.replace(",", "."));
    const company = availableCompanies.find((item) => item.ticker === selectedTicker) ?? availableCompanies[0];
    if (!company || !Number.isFinite(amount) || amount <= 0) {
      setMessage("Ingresá un monto válido mayor a cero.");
      return;
    }

    const nextPosition: Holding = {
      name: company.name,
      ticker: company.ticker,
      sector: company.sector,
      invested: amount,
      day: company.change,
      score: company.score,
    };

    setSubmitting(true);
    setMessage("Calculando nueva asignación…");
    try {
      const summary = await calculateAllocation([...positions, nextPosition]);
      setPositions((current) => [...current, nextPosition]);
      setAllocationSummary(summary);
      setInvestment("");
      setMessage(`${company.name} fue añadida. El backend recalculó los porcentajes.`);
      const next = availableCompanies.find((item) => item.ticker !== company.ticker);
      if (next) setSelectedTicker(next.ticker);
    } catch {
      setMessage("No pudimos calcular la asignación. Verificá que el backend esté activo.");
    } finally {
      setSubmitting(false);
    }
  };

  return <div className="portfolio-layout">
    <section className="portfolio-overview">
      <div className="portfolio-summary">
        <div><span>Capital invertido</span><strong>{formatMoney(totalInvested)}</strong></div>
        <div><span>Activos</span><strong>{allocationSummary.assetCount}</strong></div>
        <button type="button" aria-expanded={adding} onClick={() => { setAdding((current) => !current); setMessage(""); }}>{adding ? "Cerrar" : "+ Añadir empresa"}</button>
      </div>
      <div className={`portfolio-add${adding ? " open" : ""}`} aria-hidden={!adding} inert={!adding}>
        <form onSubmit={addPosition}>
          <label htmlFor="portfolio-company">Empresa<select id="portfolio-company" value={selectedTicker} onChange={(event) => setSelectedTicker(event.target.value)} disabled={availableCompanies.length === 0}>{availableCompanies.map((company) => <option value={company.ticker} key={company.ticker}>{company.ticker} · {company.name}</option>)}</select></label>
          <label htmlFor="portfolio-investment">Monto invertido (USD)<input id="portfolio-investment" type="number" min="1" step="0.01" inputMode="decimal" value={investment} onChange={(event) => setInvestment(event.target.value)} placeholder="50000" /></label>
          <button type="submit" disabled={availableCompanies.length === 0 || submitting}>{submitting ? "Calculando…" : "Añadir posición"}</button>
        </form>
        <p className={message.startsWith("Ingresá") || message.startsWith("No pudimos") ? "negative" : "positive"} role="status">{message || (availableCompanies.length === 0 ? "Todas las empresas disponibles ya están en el portfolio." : "El peso porcentual lo calcula el backend sobre el capital total invertido.")}</p>
      </div>
    </section>
    <section className="allocation-panel"><div className="panel-heading"><div><span>Asignación</span><strong>Distribución por activo</strong></div><span className="positive">100% invertido</span></div><div className="allocation-content"><div><div className="donut" style={{ "--allocation-gradient": `conic-gradient(${donutGradient})` } as CSSProperties} role="img" aria-label={`Asignación por activo: ${assetAllocations.map((item) => `${item.ticker} ${formatPercent(item.percent)}`).join(", ")}`}><span><strong>{allocationSummary.assetCount}</strong>activos</span></div><div className="asset-legend">{assetAllocations.map((asset) => <span key={asset.ticker}><i style={{ background: asset.color }} />{asset.ticker} <b>{formatPercent(asset.percent)}</b></span>)}</div></div><div className="sector-breakdown"><small>Por sector</small><ul>{sectorAllocations.map((allocation) => <li key={allocation.sector}><i style={{ background: allocation.color }} />{allocation.sector}<b>{formatPercent(allocation.percent)}</b></li>)}</ul></div></div></section>
    <section className="performance-panel"><div className="panel-heading"><div><span>Rendimiento</span><strong>Portfolio vs. S&amp;P 500</strong></div><span>Base 100</span></div><PortfolioPerformanceChart positions={positions} /></section>
    <section className="holdings-panel"><div className="panel-heading"><div><span>Posiciones</span><strong>{allocationSummary.assetCount} {allocationSummary.assetCount === 1 ? "activo" : "activos"}</strong></div><button type="button">Exportar CSV</button></div><div className="holdings-table"><div className="holding-row holding-head"><span>Empresa</span><span>Sector</span><span>Invertido</span><span>Peso</span><span>Hoy</span><span>Score</span><span>Acción</span></div>{positions.map((position) => <div className="holding-row" key={position.ticker}><span><i>{position.ticker[0]}</i><b>{position.name}<small>{position.ticker}</small></b></span><span data-label="Sector">{position.sector}</span><span data-label="Invertido">{formatMoney(position.invested)}</span><span data-label="Peso">{formatPercent(allocationByTicker.get(position.ticker) ?? 0)}</span><span data-label="Hoy" className={position.day.startsWith("+") ? "positive" : "negative"}>{position.day}</span><span data-label="Score"><b>{position.score}</b><small>/100</small></span><span data-label="Acción"><button className="remove-position" type="button" disabled={submitting} onClick={() => removePosition(position.ticker)} aria-label={`Eliminar ${position.name}`}>Eliminar</button></span></div>)}</div></section>
  </div>;
}

function PortfolioPerformanceChart({ positions }: { positions: Holding[] }) {
  const [range, setRange] = useState<ChartRange>("5y");
  const [series, setSeries] = useState<ChartSeries[]>([]);
  const [status, setStatus] = useState<"loading" | "ready" | "error">("loading");
  useEffect(() => {
    if (positions.length === 0) { setSeries([]); setStatus("ready"); return; }
    const controller = new AbortController();
    setStatus("loading");
    const tickers = [...positions.map((position) => position.ticker), "SPY"];
    Promise.all(tickers.map(async (ticker) => {
      const response = await fetch(`/api/companies/${ticker}/history?range=${range}`, { cache: "no-store", signal: controller.signal });
      if (!response.ok) throw new Error("history-unavailable");
      const body = await response.json() as { points: PricePoint[] };
      return [ticker, body.points] as const;
    })).then((datasets) => {
      const data = new Map(datasets);
      const spy = data.get("SPY") ?? [];
      const commonDates = spy.map((point) => point.date).filter((date) => positions.every((position) => data.get(position.ticker)?.some((point) => point.date === date)));
      if (commonDates.length < 2) { setSeries([]); setStatus("ready"); return; }
      const total = positions.reduce((sum, position) => sum + position.invested, 0);
      const maps = new Map(positions.map((position) => [position.ticker, new Map((data.get(position.ticker) ?? []).map((point) => [point.date, point.price]))]));
      const bases = new Map(positions.map((position) => [position.ticker, maps.get(position.ticker)!.get(commonDates[0])!]));
      const portfolioPoints = commonDates.map((date) => ({ date, price: positions.reduce((sum, position) => sum + position.invested / total * (maps.get(position.ticker)!.get(date)! / bases.get(position.ticker)! * 100), 0) }));
      const spyMap = new Map(spy.map((point) => [point.date, point.price])); const spyBase = spyMap.get(commonDates[0])!;
      setSeries([{ label: "Tu portfolio", points: portfolioPoints }, { label: "SPY · S&P 500", comparison: true, points: commonDates.map((date) => ({ date, price: spyMap.get(date)! / spyBase * 100 })) }]);
      setStatus("ready");
    }).catch((error: unknown) => { if (error instanceof DOMException && error.name === "AbortError") return; setSeries([]); setStatus("error"); });
    return () => controller.abort();
  }, [positions, range]);

  return <div className="portfolio-performance">{status === "loading" ? <p className="chart-state" role="status">Calculando ambas curvas con precios reales…</p> : status === "error" ? <p className="chart-state negative" role="alert">No se pudo construir la comparación real.</p> : <PriceChart compact externalSeries={series} externalRange={range} onRangeChange={setRange} subtitle="índice base 100" />}<small>Pesos actuales; no representa las fechas ni el precio real de compra de cada posición.</small></div>;
}

function Watchlist() {
  const [watched, setWatched] = useState(companies);
  const [adding, setAdding] = useState(false);
  const available = companies.filter((company) => !watched.some((item) => item.ticker === company.ticker));
  return <section className="watchlist-panel"><div className="panel-heading"><div><span>Seguimiento</span><strong>Empresas observadas</strong></div><button type="button" onClick={() => setAdding((current) => !current)}>{adding ? "Cerrar" : "+ Agregar empresa"}</button></div>
    {adding ? <div className="watchlist-add">{available.length ? available.map((company) => <button type="button" key={company.ticker} onClick={() => { setWatched((current) => [...current, company]); setAdding(false); }}>+ {company.ticker} · {company.name}</button>) : <span>Todas las empresas disponibles ya están añadidas.</span>}</div> : null}
    <div className="watchlist-grid">{watched.map((company, index) => <article key={company.ticker}><Link href={`/empresa/${company.ticker.toLowerCase()}`}><span className="watch-index">{String(index + 1).padStart(2, "0")}</span><div><strong>{company.name}</strong><span>{company.ticker} · {company.sector}</span></div><div><strong>{company.price}</strong><span className={company.change.startsWith("+") ? "positive" : "negative"}>{company.change}</span></div><div><strong>{company.score}</strong><span>Spread Score</span></div><span aria-hidden="true">→</span></Link><button className="watch-remove" type="button" onClick={() => setWatched((current) => current.filter((item) => item.ticker !== company.ticker))} aria-label={`Eliminar ${company.name} de la watchlist`}>×</button></article>)}</div>
  </section>;
}

function Comparator({ profile }: { profile: InvestorProfile }) {
  const [selected, setSelected] = useState<Company[]>([]);
  const [query, setQuery] = useState("");
  const [focused, setFocused] = useState(false);
  const available = useMemo(() => {
    const normalized = query.trim().toLowerCase();
    const selectedTickers = new Set(selected.map((company) => company.ticker));
    return companies.filter((company) => !selectedTickers.has(company.ticker)
      && (!normalized || `${company.name} ${company.ticker}`.toLowerCase().includes(normalized)));
  }, [query, selected]);
  const normalizedQuery = query.trim().toLowerCase();
  const candidate = normalizedQuery
    ? available.find((company) => company.ticker.toLowerCase() === normalizedQuery) ?? available[0]
    : undefined;
  const atLimit = selected.length >= 4;

  const addCompany = (company: Company | undefined) => {
    if (!company || atLimit) return;
    setSelected((current) => [...current, company]);
    setQuery("");
    setFocused(false);
  };

  const submitCompany = (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    addCompany(candidate);
  };

  const rows: Array<[string, (company: Company) => number, string]> = [
    ["Spread Score", (company) => company.score, ""],
    [`Profile Match · ${profile}`, (company) => company.profileMatches[profile], "%"],
    ["Calidad", (company) => company.metrics.quality, ""],
    ["Crecimiento", (company) => company.metrics.growth, ""],
    ["Rentabilidad", (company) => company.metrics.profitability, ""],
    ["Valuación", (company) => company.metrics.valuation, ""],
    ["Salud financiera", (company) => company.metrics.financialHealth, ""],
    ["Riesgo", (company) => company.metrics.risk, ""],
  ];
  const metricValues = (company: Company) => Object.values(company.metrics);
  const comparisonStyle = { gridTemplateColumns: `minmax(9rem, 1.1fr) repeat(${selected.length}, minmax(10rem, 1fr))` } as CSSProperties;

  return <section className="compare-panel">
    <div className="compare-builder">
      <div><span>Construí la comparación</span><h2>Empresas a comparar</h2><p>Buscá por ticker o nombre y añadí hasta cuatro compañías.</p></div>
      <form className="compare-search" onSubmit={submitCompany}>
        <label htmlFor="compare-company">Ticker o empresa</label>
        <div><input id="compare-company" value={query} onChange={(event) => setQuery(event.target.value)} onFocus={() => setFocused(true)} onBlur={() => window.setTimeout(() => setFocused(false), 150)} placeholder="AAPL, Microsoft…" autoComplete="off" /><button type="submit" disabled={!candidate || atLimit}>+ Añadir</button></div>
        {focused && query && !atLimit ? <div className="compare-results" role="listbox" aria-label="Empresas disponibles">{available.map((company) => <button key={company.ticker} type="button" role="option" onMouseDown={() => addCompany(company)}><span>{company.ticker}</span><strong>{company.name}</strong><small>{company.exchange}</small></button>)}{available.length === 0 ? <p>No hay coincidencias disponibles.</p> : null}</div> : null}
      </form>
    </div>
    {selected.length > 0 ? <>
      <div className="compare-top"><div><span>Firma comparada</span><h2>Perfil fundamental</h2><p>{selected.length > 2 ? "El gráfico muestra las dos primeras; la tabla compara todas las seleccionadas." : "Las líneas muestran diferencias relativas; la tabla conserva los valores exactos."}</p></div><RadarChart values={metricValues(selected[0])} compare={selected[1] ? metricValues(selected[1]) : undefined} /></div>
      <div className="comparison-table" role="table" aria-label="Comparación fundamental">
        <div className="compare-row compare-head" style={comparisonStyle}><span>Métrica</span>{selected.map((company) => <span key={company.ticker}><i>{company.ticker[0]}</i><b>{company.name}</b><small>{company.ticker}</small><button type="button" onClick={() => setSelected((current) => current.filter((item) => item.ticker !== company.ticker))} aria-label={`Quitar ${company.name}`}>×</button></span>)}</div>
        {rows.map(([label, readValue, suffix]) => <div className="compare-row" style={comparisonStyle} key={label}><span>{label}</span>{selected.map((company) => { const value = readValue(company); return <strong className={value < 60 ? "negative" : value >= 80 ? "positive" : ""} key={`${label}-${company.ticker}`}>{value}{suffix}</strong>; })}</div>)}
      </div>
    </> : <div className="compare-empty"><span>+</span><strong>Añadí tu primera empresa</strong><p>La tabla comparativa aparecerá acá.</p></div>}
    {atLimit ? <p className="compare-limit" role="status">Límite de cuatro empresas alcanzado. Quitá una para añadir otra.</p> : null}
  </section>;
}
