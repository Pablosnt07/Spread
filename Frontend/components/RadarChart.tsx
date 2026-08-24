export function RadarChart({ values = [86, 72, 91, 58, 76, 63], compare }: { values?: number[]; compare?: number[] }) {
  const center = 100;
  const radius = 72;
  const axes = ["Quality", "Growth", "Profitability", "Value", "Financial Health", "Risk"];
  const labels = [
    { x: 100, y: 10, anchor: "middle" },
    { x: 191, y: 53, anchor: "end" },
    { x: 191, y: 154, anchor: "end" },
    { x: 100, y: 207, anchor: "middle" },
    { x: 9, y: 154, anchor: "start" },
    { x: 9, y: 53, anchor: "start" },
  ] as const;
  const polygon = (source: number[]) => source.map((value, index) => {
    const angle = -Math.PI / 2 + (Math.PI * 2 * index) / 6;
    const r = radius * value / 100;
    return `${(center + Math.cos(angle) * r).toFixed(3)},${(center + Math.sin(angle) * r).toFixed(3)}`;
  }).join(" ");

  return (
    <figure className="radar">
      <svg viewBox="0 0 200 216" role="img" aria-label={`Fundamental signature: ${axes.map((axis, index) => `${axis} ${values[index]}`).join(", ")}`}>
        {[25, 50, 75, 100].map((level) => <polygon key={level} className="radar-grid" points={polygon(Array(6).fill(level))} />)}
        {axes.map((_, index) => { const angle = -Math.PI / 2 + Math.PI * 2 * index / 6; return <line className="radar-axis" key={index} x1="100" y1="100" x2={(100 + Math.cos(angle) * radius).toFixed(3)} y2={(100 + Math.sin(angle) * radius).toFixed(3)} />; })}
        {compare && <polygon className="radar-shape compare" points={polygon(compare)} />}
        <polygon className="radar-shape" points={polygon(values)} />
        {values.map((value, index) => { const angle = -Math.PI / 2 + Math.PI * 2 * index / 6; const r = radius * value / 100; return <circle key={index} cx={(100 + Math.cos(angle) * r).toFixed(3)} cy={(100 + Math.sin(angle) * r).toFixed(3)} r="2.5" />; })}
        {axes.map((axis, index) => <text className="radar-label" key={axis} x={labels[index].x} y={labels[index].y} textAnchor={labels[index].anchor}>{axis}</text>)}
      </svg>
      <figcaption>{axes.map((axis) => <span key={axis}>{axis}</span>)}</figcaption>
    </figure>
  );
}
