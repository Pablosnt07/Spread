import Link from "next/link";

export function Nav({ active = "" }: { active?: string }) {
  const links = [
    ["Mercado", "/"],
    ["Comparador", "/workspace?tab=comparador"],
    ["Portfolio", "/workspace?tab=portfolio"],
    ["Watchlist", "/workspace?tab=watchlist"],
  ];

  return (
    <header className="topbar">
      <Link className="wordmark" href="/" aria-label="Spread, inicio">SPREAD<span>.</span></Link>
      <nav className="desktop-nav" aria-label="Navegación principal">
        {links.map(([label, href]) => (
          <Link className={active === label.toLowerCase() ? "active" : ""} href={href} key={label}>{label}</Link>
        ))}
      </nav>
      <details className="mobile-menu">
        <summary aria-label="Abrir navegación"><i /><i /></summary>
        <nav aria-label="Navegación móvil">
          {links.map(([label, href]) => <Link href={href} key={label}>{label}</Link>)}
        </nav>
      </details>
    </header>
  );
}
