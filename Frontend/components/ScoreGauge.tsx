type ScoreGaugeProps = {
  score?: number;
  label?: string;
  compact?: boolean;
  loading?: boolean;
};

export function ScoreGauge({ score, label = "Spread Score", compact = false, loading = false }: ScoreGaugeProps) {
  const value = Math.max(0, Math.min(100, score ?? 0));
  const length = 251.2;
  const offset = length - (length * value) / 100;
  const angle = -180 + value * 1.8;
  const radians = (angle * Math.PI) / 180;
  const dotX = (100 + Math.cos(radians) * 80).toFixed(3);
  const dotY = (100 + Math.sin(radians) * 80).toFixed(3);

  return (
    <figure className={`score-gauge ${compact ? "compact" : ""}`} aria-label={`${label}: ${score ?? "sin calcular"} de 100`}>
      <svg viewBox="0 0 200 116" role="img" aria-hidden="true">
        <path className="gauge-track" d="M 20 100 A 80 80 0 0 1 180 100" />
        <path className={`gauge-value ${loading ? "loading" : ""}`} style={{ strokeDashoffset: score === undefined ? length : offset }} d="M 20 100 A 80 80 0 0 1 180 100" />
        {Array.from({ length: 21 }).map((_, index) => {
          const tickAngle = Math.PI + (Math.PI * index) / 20;
          const inner = index % 5 === 0 ? 70 : 74;
          const x1 = (100 + Math.cos(tickAngle) * inner).toFixed(3);
          const y1 = (100 + Math.sin(tickAngle) * inner).toFixed(3);
          const x2 = (100 + Math.cos(tickAngle) * 79).toFixed(3);
          const y2 = (100 + Math.sin(tickAngle) * 79).toFixed(3);
          return <line key={index} x1={x1} y1={y1} x2={x2} y2={y2} className="gauge-tick" />;
        })}
        {score !== undefined && !loading && <circle className="gauge-dot" cx={dotX} cy={dotY} r="3.6" />}
      </svg>
      <figcaption>
        <span>{label}</span>
        <strong>{loading ? "···" : score ?? "—"}<small>/100</small></strong>
      </figcaption>
      <div className="gauge-scale" aria-hidden="true"><span>0</span><span>50</span><span>100</span></div>
    </figure>
  );
}
