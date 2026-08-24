"use client";

import { useEffect, useMemo, useState } from "react";
import { useRouter } from "next/navigation";
import { companies } from "@/lib/data";

const demoCompanies = [companies[0], companies[3], companies[1], companies[2]];
type DemoPhase = "typing" | "holding" | "deleting";

export function CompanySearch({ onScore }: { onScore: (score?: number, loading?: boolean) => void }) {
  const [query, setQuery] = useState("");
  const [focused, setFocused] = useState(false);
  const [demoIndex, setDemoIndex] = useState(0);
  const [demoLength, setDemoLength] = useState(0);
  const [demoPhase, setDemoPhase] = useState<DemoPhase>("typing");
  const [reducedMotion, setReducedMotion] = useState(false);
  const router = useRouter();
  const filtered = useMemo(() => companies.filter((company) => `${company.name} ${company.ticker}`.toLowerCase().includes(query.toLowerCase())).slice(0, 4), [query]);
  const demoCompany = demoCompanies[demoIndex];
  const demoActive = !focused && query.length === 0;

  useEffect(() => {
    const mediaQuery = window.matchMedia("(prefers-reduced-motion: reduce)");
    const updatePreference = () => setReducedMotion(mediaQuery.matches);
    updatePreference();
    mediaQuery.addEventListener("change", updatePreference);
    return () => mediaQuery.removeEventListener("change", updatePreference);
  }, []);

  useEffect(() => {
    if (!demoActive) return;

    if (reducedMotion) {
      setDemoLength(demoCompany.name.length);
      onScore(demoCompany.score, false);
      return;
    }

    const atWordEnd = demoLength === demoCompany.name.length;
    const atWordStart = demoLength === 0;
    const delay = demoPhase === "holding" ? 1900 : demoPhase === "deleting" ? 45 : atWordStart ? 850 : 90;
    const timer = window.setTimeout(() => {
      if (demoPhase === "typing" && !atWordEnd) {
        setDemoLength((length) => length + 1);
        return;
      }

      if (demoPhase === "typing") {
        onScore(demoCompany.score, false);
        setDemoPhase("holding");
        return;
      }

      if (demoPhase === "holding") {
        onScore(undefined, false);
        setDemoPhase("deleting");
        return;
      }

      if (!atWordStart) {
        setDemoLength((length) => length - 1);
        return;
      }

      setDemoIndex((index) => (index + 1) % demoCompanies.length);
      setDemoPhase("typing");
    }, delay);

    return () => window.clearTimeout(timer);
  }, [demoActive, demoCompany, demoLength, demoPhase, onScore, reducedMotion]);

  const stopDemo = () => {
    setFocused(true);
    setDemoLength(0);
    setDemoPhase("typing");
    onScore(undefined, false);
  };

  return (
    <div className="search-shell">
      <label htmlFor="company-search">Buscar una empresa</label>
      <div className="search-control">
        <svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="11" cy="11" r="6.5" /><path d="m16 16 4 4" /></svg>
        {demoActive && <span className="search-ghost" aria-hidden="true">{demoCompany.name.slice(0, demoLength)}<i>_</i></span>}
        <input
          id="company-search"
          value={query}
          onChange={(event) => { setQuery(event.target.value); onScore(undefined, false); }}
          onFocus={stopDemo}
          onBlur={() => window.setTimeout(() => setFocused(false), 150)}
          placeholder={focused ? "Nombre o ticker…" : ""}
          autoComplete="off"
        />
        {query && <button type="button" onClick={() => setQuery("")} aria-label="Limpiar búsqueda">×</button>}
      </div>
      {focused && (
        <div className="search-results" role="listbox" aria-label="Resultados de empresas">
          {(query ? filtered : companies).map((company) => (
            <button key={company.ticker} type="button" role="option" onMouseDown={() => router.push(`/empresa/${company.ticker.toLowerCase()}`)}>
              <span className="company-mark">{company.ticker.slice(0, 1)}</span>
              <span><strong>{company.name}</strong><small>{company.ticker} · {company.exchange} · {company.sector}</small></span>
              <span className="quote"><strong>{company.price}</strong><small className={company.change.startsWith("+") ? "positive" : "negative"}>{company.change}</small></span>
            </button>
          ))}
          {query && filtered.length === 0 && <p>No encontramos esa empresa. Probá con nombre o ticker.</p>}
        </div>
      )}
    </div>
  );
}
