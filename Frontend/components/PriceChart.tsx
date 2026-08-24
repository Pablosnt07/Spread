"use client";

import { useEffect, useMemo, useState } from "react";

export type ChartRange = "ytd" | "1y" | "3y" | "5y" | "max";
export type PricePoint = { date: string; price: number };
export type ChartSeries = { label: string; points: PricePoint[]; comparison?: boolean };
const ranges: Array<[ChartRange, string]> = [["ytd", "YTD"], ["1y", "1A"], ["3y", "3A"], ["5y", "5A"], ["max", "Máx."]];

function pathFor(values: number[], height: number, min: number, max: number) {
  const spread = max - min || 1;
  return values.map((value, index) => `${index === 0 ? "M" : "L"}${(index / Math.max(values.length - 1, 1) * 760).toFixed(1)} ${(height - (value - min) / spread * height).toFixed(1)}`).join(" ");
}

export function PriceChart({ ticker, compact = false, externalSeries, externalRange, onRangeChange, subtitle = "USD · cierre diario" }: {
  ticker?: string; compact?: boolean; externalSeries?: ChartSeries[]; externalRange?: ChartRange; onRangeChange?: (range: ChartRange) => void; subtitle?: string;
}) {
  const [localRange, setLocalRange] = useState<ChartRange>("5y");
  const [points, setPoints] = useState<PricePoint[]>([]);
  const [status, setStatus] = useState<"loading" | "ready" | "empty" | "error">(ticker ? "loading" : "empty");
  const range = externalRange ?? localRange;
  const series = externalSeries ?? (ticker ? [{ label: ticker, points }] : []);

  useEffect(() => {
    if (!ticker || externalSeries) return;
    const controller = new AbortController();
    setStatus("loading");
    fetch(`/api/companies/${encodeURIComponent(ticker)}/history?range=${range}`, { cache: "no-store", signal: controller.signal })
      .then(async (response) => {
        if (!response.ok) throw new Error("history-unavailable");
        const body = await response.json() as { points?: PricePoint[] };
        const next = Array.isArray(body.points) ? body.points.filter((point) => Number.isFinite(point.price)) : [];
        setPoints(next); setStatus(next.length > 1 ? "ready" : "empty");
      })
      .catch((error: unknown) => { if (error instanceof DOMException && error.name === "AbortError") return; setStatus("error"); setPoints([]); });
    return () => controller.abort();
  }, [externalSeries, range, ticker]);

  const geometry = useMemo(() => {
    const all = series.flatMap((item) => item.points.map((point) => point.price));
    if (all.length < 2) return null;
    const rawMin = Math.min(...all); const rawMax = Math.max(...all);
    const padding = Math.max((rawMax - rawMin) * 0.08, rawMax * 0.01); const height = compact ? 170 : 250;
    return { height, paths: series.map((item) => ({ ...item, path: pathFor(item.points.map((point) => point.price), height, rawMin - padding, rawMax + padding) })) };
  }, [compact, series]);
  const updateRange = (next: ChartRange) => { setLocalRange(next); onRangeChange?.(next); };
  const basePoints = series[0]?.points ?? [];
  const axis = basePoints.length > 1 ? [basePoints[0], basePoints[Math.floor((basePoints.length - 1) / 2)], basePoints.at(-1)!] : [];

  return <figure className={`price-chart ${compact ? "chart-compact" : ""}`}>
    <div className="chart-title"><span>{externalSeries ? "Rendimiento comparado" : "Precio histórico"} <small>{subtitle}</small></span><div>{ranges.map(([value, label]) => <button className={range === value ? "selected" : ""} onClick={() => updateRange(value)} type="button" key={value}>{label}</button>)}</div></div>
    {externalSeries ? <div className="chart-legend">{externalSeries.map((item) => <span className={item.comparison ? "comparison" : ""} key={item.label}><i />{item.label}</span>)}</div> : null}
    {!externalSeries && status === "loading" ? <div className="chart-state" role="status">Cargando precios reales…</div> : null}
    {!externalSeries && status === "error" ? <div className="chart-state negative" role="alert">No pudimos obtener el histórico real.</div> : null}
    {!geometry && (externalSeries || status === "empty") ? <div className="chart-state">No hay suficientes cotizaciones para este rango.</div> : null}
    {geometry ? <><svg viewBox={`0 0 760 ${compact ? 190 : 280}`} preserveAspectRatio="none" role="img" aria-label={`${series.map((item) => item.label).join(" versus ")}, rango ${range}`}>
      {[0, 1, 2, 3, 4].map((line) => <line className="chart-guide" key={line} x1="0" x2="760" y1={20 + line * (geometry.height / 4)} y2={20 + line * (geometry.height / 4)} />)}
      {geometry.paths.map((item) => <path key={item.label} className={item.comparison ? "chart-line chart-line-comparison" : "chart-line"} d={item.path} />)}
    </svg><div className="chart-axis" aria-hidden="true">{axis.map((point) => <span key={point.date}>{new Intl.DateTimeFormat("es-AR", { month: "short", year: "2-digit", timeZone: "UTC" }).format(new Date(`${point.date}T00:00:00Z`))}</span>)}</div></> : null}
  </figure>;
}
