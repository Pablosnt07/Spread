import type { Metadata } from "next";
import Link from "next/link";
import { Nav } from "@/components/Nav";

export const metadata: Metadata = {
  title: "Metodología — Spread",
  description: "Cómo Spread calcula un score fundamental determinista, explicable y comparable.",
};

const dimensions = [
  { name: "Quality", weight: 22, note: "Calidad y eficiencia del negocio" },
  { name: "Growth", weight: 18, note: "Evolución sostenible de sus resultados" },
  { name: "Profitability", weight: 18, note: "Capacidad de convertir ventas y capital en ganancias" },
  { name: "Valuation", weight: 18, note: "Precio relativo a fundamentales y pares" },
  { name: "Financial Health", weight: 14, note: "Solvencia, liquidez y estructura financiera" },
  { name: "Risk", weight: 10, note: "Un valor más alto representa menor riesgo" },
] as const;

const confidence = [
  { name: "Coverage", label: "Cobertura de datos", weight: 45 },
  { name: "Freshness", label: "Vigencia de los datos", weight: 20 },
  { name: "Peer quality", label: "Calidad de la comparación", weight: 20 },
  { name: "Consistency", label: "Consistencia histórica", weight: 15 },
] as const;

export default function MethodologyPage() {
  return (
    <>
      <Nav active="metodología" />
      <main className="methodology-shell">
        <section className="methodology-hero" aria-labelledby="methodology-title">
          <div>
            <p className="methodology-kicker">Metodología pública · modelo v0.1.0</p>
            <h1 id="methodology-title">Cómo funciona Spread</h1>
            <p>
              Spread transforma datos financieros públicos en una lectura fundamental
              determinista, comparable y explicable. El resultado resume evidencia; no
              predice precios ni recomienda comprar o vender.
            </p>
          </div>
          <dl className="methodology-stats" aria-label="Parámetros principales del modelo">
            <div><dt>Escala</dt><dd>0–100</dd><small>Mayor es mejor</small></div>
            <div><dt>Dimensiones</dt><dd>6</dd><small>Pilares de análisis</small></div>
            <div><dt>Cobertura mínima</dt><dd>70%</dd><small>Para score provisional</small></div>
            <div><dt>Confianza mínima</dt><dd>40</dd><small>Para publicar</small></div>
          </dl>
        </section>

        <section className="methodology-section" aria-labelledby="weights-title">
          <header>
            <span>01 / Company Score</span>
            <h2 id="weights-title">Seis dimensiones, una lectura común</h2>
            <p>
              El Company Score representa la empresa, no el perfil del inversor. Cada
              dimensión se calcula en una escala interna de 0 a 100.
            </p>
          </header>
          <div className="weight-list">
            {dimensions.map((dimension, index) => (
              <div className="weight-row" key={dimension.name}>
                <span>{String(index + 1).padStart(2, "0")}</span>
                <div>
                  <strong>{dimension.name}</strong>
                  <small>{dimension.note}</small>
                </div>
                <i aria-hidden="true"><b style={{ width: `${(dimension.weight / 22) * 100}%` }} /></i>
                <em>{dimension.weight}%</em>
              </div>
            ))}
          </div>
        </section>

        <section className="methodology-section methodology-criteria" aria-labelledby="criteria-title">
          <header>
            <span>02 / Criterio</span>
            <h2 id="criteria-title">Absolutos y pares, sin ocultar los vacíos</h2>
            <p>
              Las métricas combinan anclas fundamentales con la posición de la empresa
              frente a comparables válidos.
            </p>
          </header>
          <div>
            <p className="methodology-formula"><strong>45%</strong> anclas absolutas <span>+</span> <strong>55%</strong> comparación con pares</p>
            <ol className="methodology-rules">
              <li><span>01</span><div><strong>Un dato faltante no vale cero.</strong><p>Se excluye del cálculo y se informa como no disponible.</p></div></li>
              <li><span>02</span><div><strong>Los pesos disponibles se renormalizan.</strong><p>El score divide por la suma de los pesos con evidencia válida.</p></div></li>
              <li><span>03</span><div><strong>Publicar requiere evidencia suficiente.</strong><p>Sin cobertura o confianza mínima, el estado es “datos insuficientes”.</p></div></li>
            </ol>
          </div>
        </section>

        <section className="methodology-section" aria-labelledby="confidence-title">
          <header>
            <span>03 / Confidence Score</span>
            <h2 id="confidence-title">Cuánto confiar en el resultado</h2>
            <p>
              La confianza es independiente del atractivo de la empresa: mide la calidad
              de la evidencia que sostiene el Company Score.
            </p>
          </header>
          <div className="confidence-list">
            {confidence.map((item) => (
              <div key={item.name}>
                <span><strong>{item.name}</strong><small>{item.label}</small></span>
                <i aria-hidden="true"><b style={{ width: `${(item.weight / 45) * 100}%` }} /></i>
                <em>{item.weight}%</em>
              </div>
            ))}
            <p>El umbral mínimo de publicación es 40/100. El score definitivo requiere, además, cobertura ponderada suficiente.</p>
          </div>
        </section>

        <section className="methodology-section" aria-labelledby="providers-title">
          <header>
            <span>04 / Datos</span>
            <h2 id="providers-title">Fuentes y responsabilidades</h2>
            <p>
              Spread normaliza respuestas externas antes de analizarlas. Ninguna API
              decide el score por sí sola.
            </p>
          </header>
          <div className="provider-list">
            <article>
              <div><strong>Financial Modeling Prep</strong><span>FMP · fuente principal</span></div>
              <p>Perfil y logo de la empresa, estados financieros, dividendos y actividad de mercado.</p>
            </article>
            <article>
              <div><strong>Alpha Vantage</strong><span>Fuente especializada</span></div>
              <p>Historial de transacciones de insiders específico por empresa, cuando el proveedor está configurado.</p>
            </article>
            <aside><span>Seguridad</span><p>Las claves de API permanecen en el backend y nunca se envían al navegador ni se publican en el repositorio.</p></aside>
          </div>
        </section>

        <section className="methodology-disclosure" aria-labelledby="disclosure-title">
          <span>05 / Acerca del proyecto</span>
          <h2 id="disclosure-title">Proyecto personal, educativo y sin fines de lucro.</h2>
          <div>
            <p>
              Spread es un proyecto de portfolio independiente. No está afiliado,
              patrocinado ni aprobado por Financial Modeling Prep, Alpha Vantage o las
              compañías analizadas.
            </p>
            <p>
              La información puede contener demoras o errores y no constituye
              asesoramiento financiero. Cada persona debe verificar los datos de forma
              independiente y tomar sus propias decisiones.
            </p>
          </div>
          <Link href="/">Explorar el mercado <span aria-hidden="true">→</span></Link>
        </section>
      </main>
    </>
  );
}
