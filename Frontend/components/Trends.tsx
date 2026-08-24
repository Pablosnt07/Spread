import Link from "next/link";
import { companies } from "@/lib/data";

export function Trends() {
  return (
    <section className="trends" id="tendencias" aria-labelledby="trends-title">
      <div className="section-heading">
        <div><p>Lecturas recientes</p><h2 id="trends-title">Tendencias del mercado</h2></div>
        <p>Movimientos de precio y cambios fundamentales se muestran por separado.</p>
      </div>
      <div className="trend-table" role="table" aria-label="Empresas en tendencia">
        <div className="trend-row trend-head" role="row"><span>Empresa</span><span>Sector</span><span>Precio</span><span>Hoy</span><span>Spread Score</span></div>
        {companies.map((company) => (
          <Link href={`/empresa/${company.ticker.toLowerCase()}`} className="trend-row" role="row" key={company.ticker}>
            <span><i>{company.ticker[0]}</i><b>{company.name}<small>{company.ticker} · {company.exchange}</small></b></span>
            <span data-label="Sector">{company.sector}</span><span data-label="Precio">{company.price}</span>
            <span data-label="Hoy" className={company.change.startsWith("+") ? "positive" : "negative"}>{company.change}</span>
            <span data-label="Spread Score"><strong>{company.score}</strong><em>/100</em></span>
          </Link>
        ))}
      </div>
      <p className="disclaimer">Spread es una herramienta informativa. No constituye asesoramiento ni una recomendación de inversión.</p>
    </section>
  );
}
