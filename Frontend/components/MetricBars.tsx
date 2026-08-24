import { appleMetrics } from "@/lib/data";

export function MetricBars() {
  return (
    <div className="metric-bars">
      {appleMetrics.map(([key, label, value]) => (
        <div className="metric-bar" key={key}>
          <div><span>{label}</span><strong>{value}</strong></div>
          <div className="bar-track" role="meter" aria-label={label} aria-valuemin={0} aria-valuemax={100} aria-valuenow={value}><i style={{ width: `${value}%` }} /></div>
        </div>
      ))}
    </div>
  );
}
