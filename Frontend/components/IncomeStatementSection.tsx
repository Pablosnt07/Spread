import type { CompanyFinancials, FinancialPeriod } from "@/lib/financials";

const metrics = [
  { key: "revenue", label: "Ingresos", className: "revenue" },
  { key: "grossProfit", label: "Ganancia bruta", className: "gross" },
  { key: "operatingIncome", label: "Resultado operativo", className: "operating" },
  { key: "netIncome", label: "Ganancia neta", className: "net" },
] as const;

type MetricKey = (typeof metrics)[number]["key"];

function formatCompact(value: number, currency: string | null) {
  const absolute = Math.abs(value);
  const divisor = absolute >= 1_000_000_000 ? 1_000_000_000 : absolute >= 1_000_000 ? 1_000_000 : 1;
  const suffix = divisor === 1_000_000_000 ? "B" : divisor === 1_000_000 ? "M" : "";
  const formatted = (value / divisor).toLocaleString("es-AR", { maximumFractionDigits: 1 });
  return `${currency ?? "USD"} ${formatted}${suffix}`;
}

function formatOptional(value: number | null, currency: string | null, missing = "—") {
  return value === null ? missing : formatCompact(value, currency);
}

export function IncomeStatementSection({ financials }: { financials: CompanyFinancials | null }) {
  const periods = financials?.periods
    .filter((period) => metrics.some((metric) => period[metric.key] !== null))
    .slice(0, 5);

  if (!periods?.length) return null;

  const latest = periods[0];
  const chartPeriods = [...periods].reverse();
  const currency = latest.reportedCurrency;

  return (
    <section className="income-section" aria-labelledby="income-title">
      <div className="section-heading">
        <div><p>Estado de resultados</p><h2 id="income-title">Ingresos y resultados</h2></div>
        <p>Balance anual normalizado desde FMP. Los datos faltantes se omiten y no se interpretan como cero.</p>
      </div>
      <div className="income-summary">
        {metrics.map((metric) => <div key={metric.key}><span>{metric.label}</span><strong>{formatOptional(latest[metric.key], currency)}</strong><small>FY {latest.fiscalYear}</small></div>)}
      </div>
      <IncomeChart periods={chartPeriods} currency={currency} />
      <IncomeChart periods={chartPeriods.slice(-3)} currency={currency} compact />
    </section>
  );
}

function IncomeChart({ periods, currency, compact = false }: { periods: FinancialPeriod[]; currency: string | null; compact?: boolean }) {
  const width = compact ? 430 : 760;
  const height = 310;
  const plot = { left: 58, right: 18, top: 28, bottom: 55 };
  const plotWidth = width - plot.left - plot.right;
  const plotHeight = height - plot.top - plot.bottom;
  const values = periods.flatMap((period) => metrics.flatMap((metric) => period[metric.key] ?? []));
  const minValue = Math.min(0, ...values);
  const maxValue = Math.max(0, ...values);
  const range = maxValue - minValue || 1;
  const y = (value: number) => plot.top + ((maxValue - value) / range) * plotHeight;
  const zeroY = y(0);
  const groupWidth = plotWidth / periods.length;
  const barWidth = Math.min(23, Math.max(10, (groupWidth - 22) / metrics.length));
  const barsWidth = barWidth * metrics.length;
  const gridValues = Array.from({ length: 5 }, (_, index) => minValue + (range * index) / 4).reverse();
  const ariaSummary = periods.map((period) => `${period.fiscalYear}: ${metrics.map((metric) => `${metric.label} ${formatOptional(period[metric.key], currency, "sin dato")}`).join(", ")}`).join("; ");

  return (
    <figure className={`income-chart ${compact ? "income-chart-mobile" : "income-chart-desktop"}`}>
      <div className="income-legend">{metrics.map((metric) => <span key={metric.key}><i className={metric.className} />{metric.label}</span>)}</div>
      <svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label={`Estado de resultados anual. ${ariaSummary}`}>
        {gridValues.map((value, index) => {
          const gridY = y(value);
          return <g key={index}><line className="income-grid-line" x1={plot.left} x2={width - plot.right} y1={gridY} y2={gridY} /><text className="income-axis-label" x={plot.left - 9} y={gridY + 3} textAnchor="end">{formatCompact(value, currency).replace(`${currency ?? "USD"} `, "")}</text></g>;
        })}
        <line className="income-zero-line" x1={plot.left} x2={width - plot.right} y1={zeroY} y2={zeroY} />
        {periods.map((period, periodIndex) => {
          const startX = plot.left + periodIndex * groupWidth + (groupWidth - barsWidth) / 2;
          return <g key={`${period.fiscalYear}-${period.period}`}>
            {metrics.map((metric, metricIndex) => {
              const value = period[metric.key as MetricKey];
              if (value === null) return null;
              const valueY = y(value);
              return <rect key={metric.key} className={`income-bar ${metric.className}`} x={startX + metricIndex * barWidth} y={Math.min(valueY, zeroY)} width={Math.max(5, barWidth - 3)} height={Math.max(1, Math.abs(zeroY - valueY))} rx="2"><title>{`${period.fiscalYear} · ${metric.label}: ${formatCompact(value, currency)}`}</title></rect>;
            })}
            <text className="income-year-label" x={plot.left + periodIndex * groupWidth + groupWidth / 2} y={height - 22} textAnchor="middle">{period.fiscalYear}</text>
          </g>;
        })}
      </svg>
      <figcaption>{currency ?? "Moneda reportada"} · Períodos fiscales anuales</figcaption>
    </figure>
  );
}
