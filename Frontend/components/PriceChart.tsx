const points = [185,181,188,193,191,199,204,201,209,212,218,214,205,198,202,211,216,220,226,221,229,234,231,238,242,237,244,248,252,247,239,244,251,256,263,259,267,272,269,278,283,280,287,291,296,289,294,301,306,311,307,315,319,316,323,330];

function pathFor(values: number[], width = 760, height = 250) {
  const min = Math.min(...values) - 10;
  const max = Math.max(...values) + 10;
  return values.map((value, index) => {
    const x = (index / (values.length - 1)) * width;
    const y = height - ((value - min) / (max - min)) * height;
    return `${index === 0 ? "M" : "L"}${x.toFixed(1)} ${y.toFixed(1)}`;
  }).join(" ");
}

export function PriceChart({ compact = false }: { compact?: boolean }) {
  const path = pathFor(points, 760, compact ? 170 : 250);
  return (
    <figure className={`price-chart ${compact ? "chart-compact" : ""}`}>
      <div className="chart-title"><span>Precio histórico <small>USD · Ajustado</small></span><div><button type="button">1A</button><button type="button">3A</button><button className="selected" type="button">5A</button><button type="button">Máx.</button></div></div>
      <svg viewBox={`0 0 760 ${compact ? 190 : 280}`} preserveAspectRatio="none" role="img" aria-label="Precio histórico de Apple durante cinco años, tendencia general ascendente con períodos de volatilidad">
        {[0, 1, 2, 3, 4].map((line) => <line className="chart-guide" key={line} x1="0" x2="760" y1={30 + line * 52} y2={30 + line * 52} />)}
        <path className="chart-area" d={`${path} L760 ${compact ? 170 : 250} L0 ${compact ? 170 : 250} Z`} />
        <path className="chart-line" d={path} />
        <circle className="chart-point" cx="760" cy={compact ? 16 : 18} r="4" />
      </svg>
      <div className="chart-axis" aria-hidden="true"><span>2021</span><span>2022</span><span>2023</span><span>2024</span><span>2025</span><span>Hoy</span></div>
    </figure>
  );
}
