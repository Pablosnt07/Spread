"use client";

import { useState, type CSSProperties } from "react";

type CompanyLogoProps = {
  name: string;
  ticker: string;
  logoUrl?: string;
  brandColor?: string;
};

export function CompanyLogo({ name, ticker, logoUrl, brandColor }: CompanyLogoProps) {
  const [failedUrl, setFailedUrl] = useState<string>();
  const canShowLogo = Boolean(logoUrl && failedUrl !== logoUrl);

  return (
    <span
      className="company-logo"
      style={{ "--company-accent": brandColor ?? "var(--line)" } as CSSProperties}
      aria-label={`${name}, ${ticker}`}
    >
      {canShowLogo ? (
        <img src={logoUrl} alt={`Logo de ${name}`} onError={() => setFailedUrl(logoUrl)} />
      ) : (
        <span aria-hidden="true">{ticker.slice(0, 1)}</span>
      )}
    </span>
  );
}
