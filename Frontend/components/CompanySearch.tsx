"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { companies, searchableAssets } from "@/lib/data";
import type { CompanySearchResult } from "@/lib/company-search";

type SearchStatus = "idle" | "loading" | "ready" | "empty" | "invalid" | "rate-limited" | "error";
const SEARCH_PATTERN = /^[A-Za-z0-9 .&'-]{2,64}$/;
const suggestions = searchableAssets.slice(0, 6);
const demoCompanies = companies.slice(0, 3);

export function CompanySearch({ onScore }: { onScore: (score?: number, loading?: boolean) => void }) {
  const [query, setQuery] = useState("");
  const [focused, setFocused] = useState(false);
  const [results, setResults] = useState<CompanySearchResult[]>([]);
  const [status, setStatus] = useState<SearchStatus>("idle");
  const [demoText, setDemoText] = useState("");
  const [demoIndex, setDemoIndex] = useState(0);
  const [demoPhase, setDemoPhase] = useState<"typing" | "hold" | "deleting">("typing");
  const [userEngaged, setUserEngaged] = useState(false);
  const router = useRouter();

  useEffect(() => {
    if (userEngaged || focused || query) return;
    const demo = demoCompanies[demoIndex % demoCompanies.length];
    const text = `${demo.name} · ${demo.ticker}`;
    const delay = demoPhase === "hold" ? 1400 : demoPhase === "typing" ? 85 : 38;
    const timer = window.setTimeout(() => {
      if (demoPhase === "typing") {
        const next = text.slice(0, demoText.length + 1); setDemoText(next);
        if (next === text) { setDemoPhase("hold"); onScore(demo.score, false); }
      } else if (demoPhase === "hold") {
        setDemoPhase("deleting");
      } else {
        const next = demoText.slice(0, -1); setDemoText(next); onScore(undefined, false);
        if (!next) { setDemoIndex((current) => (current + 1) % demoCompanies.length); setDemoPhase("typing"); }
      }
    }, delay);
    return () => window.clearTimeout(timer);
  }, [demoIndex, demoPhase, demoText, focused, onScore, query, userEngaged]);

  useEffect(() => {
    const normalized = query.trim().replace(/\s+/g, " ");
    setResults([]);
    onScore(undefined, false);

    if (!normalized) {
      setStatus("idle");
      return;
    }

    if (normalized.length < 2 || !SEARCH_PATTERN.test(normalized)) {
      setStatus("invalid");
      return;
    }

    const controller = new AbortController();
    const timer = window.setTimeout(async () => {
      setStatus("loading");
      try {
        const response = await fetch(`/api/companies/search?q=${encodeURIComponent(normalized)}&limit=6`, {
          cache: "no-store",
          signal: controller.signal,
        });
        if (response.status === 429) {
          setStatus("rate-limited");
          return;
        }
        if (!response.ok) throw new Error("search-unavailable");
        const body = await response.json() as CompanySearchResult[];
        setResults(body);
        setStatus(body.length ? "ready" : "empty");
      } catch (error: unknown) {
        if (error instanceof DOMException && error.name === "AbortError") return;
        setStatus("error");
      }
    }, 320);

    return () => {
      window.clearTimeout(timer);
      controller.abort();
    };
  }, [onScore, query]);

  const openCompany = (ticker: string) => router.push(`/empresa/${ticker.toLowerCase()}`);

  return (
    <div className="search-shell">
      <label htmlFor="company-search">Buscar una empresa</label>
      <div className="search-control">
        <svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="11" cy="11" r="6.5" /><path d="m16 16 4 4" /></svg>
        {!query && !focused && !userEngaged ? <span className="search-ghost" aria-hidden="true">{demoText}<i /></span> : null}
        <input id="company-search" value={query} maxLength={64} onChange={(event) => { setUserEngaged(true); setDemoText(""); onScore(undefined, false); setQuery(event.target.value); }} onFocus={() => setFocused(true)} onBlur={() => window.setTimeout(() => setFocused(false), 150)} placeholder={userEngaged ? "Nombre o ticker…" : ""} autoComplete="off" spellCheck={false} aria-describedby="company-search-help" />
        {query && <button type="button" onClick={() => setQuery("")} aria-label="Limpiar búsqueda">×</button>}
      </div>
      <small id="company-search-help" className="search-help">2–64 caracteres · hasta 6 resultados · datos de FMP</small>
      {focused && (
        <div className="search-results" role="listbox" aria-label="Resultados de empresas">
          {!query.trim() && suggestions.map((asset) => (
            <button key={asset.ticker} type="button" role="option" onMouseDown={() => openCompany(asset.ticker)}>
              <span className="company-mark">{asset.ticker.slice(0, 1)}</span><span><strong>{asset.name}</strong><small>{asset.ticker} · {asset.exchange} · {asset.sector}</small></span><span className="quote"><strong>{asset.assetType === "etf" ? "ETF" : asset.price}</strong><small>{asset.assetType === "etf" ? "Aún no compatible" : asset.change}</small></span>
            </button>
          ))}
          {results.map((result) => (
            <button key={result.ticker} type="button" role="option" onMouseDown={() => openCompany(result.ticker)}>
              <span className="company-mark">{result.ticker.slice(0, 1)}</span><span><strong>{result.companyName}</strong><small>{result.ticker} · {result.exchange ?? "Mercado no informado"}{result.currency ? ` · ${result.currency}` : ""}</small></span><span className="quote"><strong>Analizar</strong><small>Datos reales</small></span>
            </button>
          ))}
          {status === "loading" && <p role="status">Buscando empresas…</p>}
          {status === "invalid" && <p role="status">Usá al menos 2 caracteres. Solo letras, números, espacios, punto, guion, apóstrofe y &amp;.</p>}
          {status === "empty" && <p role="status">No encontramos coincidencias. Probá con el ticker exacto.</p>}
          {status === "rate-limited" && <p role="alert">Demasiadas búsquedas seguidas. Esperá un minuto y volvé a intentar.</p>}
          {status === "error" && <p role="alert">La búsqueda no está disponible temporalmente. No se mostraron resultados simulados.</p>}
        </div>
      )}
    </div>
  );
}
