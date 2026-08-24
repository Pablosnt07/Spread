export type Company = {
  ticker: string;
  name: string;
  exchange: string;
  sector: string;
  industry: string;
  price: string;
  change: string;
  score: number;
  confidence: number;
  match: number;
  logoUrl?: string;
  brandColor?: string;
  metrics: {
    quality: number;
    growth: number;
    profitability: number;
    valuation: number;
    financialHealth: number;
    risk: number;
  };
};

export const companies: Company[] = [
  { ticker: "AAPL", name: "Apple Inc.", exchange: "NASDAQ", sector: "Tecnología", industry: "Hardware y servicios", price: "USD 231,42", change: "+1,28%", score: 74, confidence: 91, match: 82, logoUrl: "https://images.financialmodelingprep.com/symbol/AAPL.png", brandColor: "oklch(0.96 0 0)", metrics: { quality: 86, growth: 72, profitability: 91, valuation: 58, financialHealth: 76, risk: 63 } },
  { ticker: "MSFT", name: "Microsoft Corporation", exchange: "NASDAQ", sector: "Tecnología", industry: "Software", price: "USD 416,27", change: "+0,41%", score: 79, confidence: 94, match: 86, logoUrl: "https://images.financialmodelingprep.com/symbol/MSFT.png", brandColor: "oklch(0.72 0.14 145)", metrics: { quality: 88, growth: 78, profitability: 87, valuation: 64, financialHealth: 84, risk: 72 } },
  { ticker: "MELI", name: "MercadoLibre", exchange: "NASDAQ", sector: "Consumo", industry: "Comercio digital", price: "USD 2.421,18", change: "+2,37%", score: 81, confidence: 88, match: 84, logoUrl: "https://images.financialmodelingprep.com/symbol/MELI.png", brandColor: "oklch(0.88 0.17 95)", metrics: { quality: 79, growth: 91, profitability: 83, valuation: 69, financialHealth: 71, risk: 58 } },
  { ticker: "NVDA", name: "NVIDIA Corporation", exchange: "NASDAQ", sector: "Tecnología", industry: "Semiconductores", price: "USD 182,91", change: "−0,36%", score: 77, confidence: 89, match: 76, logoUrl: "https://images.financialmodelingprep.com/symbol/NVDA.png", brandColor: "oklch(0.75 0.2 135)", metrics: { quality: 84, growth: 94, profitability: 89, valuation: 49, financialHealth: 81, risk: 55 } },
];

export const appleMetrics = [
  ["Quality", "Calidad", 86],
  ["Growth", "Crecimiento", 72],
  ["Profitability", "Rentabilidad", 91],
  ["Valuation", "Valuación", 58],
  ["Financial Health", "Salud financiera", 76],
  ["Risk", "Riesgo", 63],
] as const;

export const fundamentals = [
  ["Ingresos", "USD 391,0B", "FY 2025"],
  ["Flujo de caja libre", "USD 101,5B", "TTM"],
  ["Margen operativo", "31,5%", "TTM"],
  ["ROIC", "28,7%", "TTM"],
  ["P/E", "29,4×", "TTM"],
  ["Deuda neta / EBITDA", "0,71×", "FY 2025"],
];
