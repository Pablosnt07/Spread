"use client";

import Link from "next/link";
import { useCallback, useState } from "react";
import { CompanySearch } from "./CompanySearch";
import { DataLandscape } from "./DataLandscape";
import { ScoreGauge } from "./ScoreGauge";

export function Hero() {
  const [score, setScore] = useState<number | undefined>();
  const [loading, setLoading] = useState(false);
  const handleScore = useCallback((next?: number, nextLoading = false) => { setScore(next); setLoading(nextLoading); }, []);

  return (
    <main className="hero">
      <div className="atmosphere" aria-hidden="true"><i /><i /><i /></div>
      <div className="hero-content">
        <p className="hero-kicker">Spread — Investment intelligence</p>
        <h1><span className="terminal-caret">›</span><span className="typewriter">SPREAD</span><span className="cursor">_</span></h1>
        <p className="hero-copy">Analizá empresas, entendé sus fundamentos y compará la evidencia antes de tomar una decisión.</p>
        <div className="hero-gauge"><ScoreGauge score={score} loading={loading} /></div>
        <CompanySearch onScore={handleScore} />
        <Link className="explore-link" href="/empresa/aapl">Ver análisis de ejemplo <span>→</span></Link>
      </div>
      <DataLandscape />
      <a className="scroll-cue" href="#tendencias" aria-label="Ir a tendencias">↓</a>
    </main>
  );
}
