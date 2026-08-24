const candles = Array.from({ length: 74 }, (_, index) => {
  const wave = Math.sin(index * 0.34) * 19 + Math.cos(index * 0.13) * 12;
  const trend = index * 0.35;
  const body = 5 + ((index * 7) % 15);
  return { x: index * 18 + 8, y: Number((98 - wave - trend).toFixed(3)), body, up: index % 5 !== 0 && index % 7 !== 0 };
});
const candleGroups = Array.from({ length: Math.ceil(candles.length / 6) }, (_, groupIndex) =>
  candles.slice(groupIndex * 6, groupIndex * 6 + 6),
);

export function DataLandscape() {
  return (
    <div className="data-landscape" aria-hidden="true">
      <svg viewBox="0 0 1340 250" preserveAspectRatio="none">
        <g className="topography">
          {Array.from({ length: 16 }, (_, index) => (
            <path key={index} d={`M0 ${92 + index * 10} C 160 ${32 + index * 12}, 260 ${155 + index * 4}, 430 ${88 + index * 9} S 720 ${45 + index * 11}, 890 ${105 + index * 7} S 1150 ${38 + index * 12}, 1340 ${82 + index * 8}`} />
          ))}
        </g>
        <path className="market-trace" d="M0 166 C 160 112, 270 205, 438 143 S 715 94, 902 162 S 1160 87, 1340 130" />
        <g className="candles">
          {candleGroups.map((group, groupIndex) => (
            <g
              key={groupIndex}
              className="candle-group"
              style={{
                "--delay": `${groupIndex * 35}ms`,
                "--motion-delay": `${(groupIndex % 5) * 0.34}s`,
                "--motion-duration": `${7 + (groupIndex % 4) * 0.8}s`,
              } as React.CSSProperties}
            >
              {group.map((candle) => (
                <g key={candle.x} className={candle.up ? "up" : "down"}>
                  <line x1={candle.x} y1={candle.y - 8} x2={candle.x} y2={candle.y + candle.body + 9} />
                  <rect x={candle.x - 3} y={candle.y} width="6" height={candle.body} rx="1" />
                </g>
              ))}
            </g>
          ))}
        </g>
      </svg>
    </div>
  );
}
