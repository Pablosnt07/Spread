export type FinancialPeriod = {
  periodEnd: string;
  fiscalYear: string;
  period: string;
  filingDate: string | null;
  reportedCurrency: string | null;
  revenue: number | null;
  grossProfit: number | null;
  operatingIncome: number | null;
  netIncome: number | null;
  ebitda: number | null;
  dilutedEps: number | null;
  dilutedSharesOutstanding: number | null;
  cashAndCashEquivalents: number | null;
  totalDebt: number | null;
  totalAssets: number | null;
  totalEquity: number | null;
  currentAssets: number | null;
  currentLiabilities: number | null;
  operatingCashFlow: number | null;
  capitalExpenditure: number | null;
  freeCashFlow: number | null;
};

export type CompanyFinancials = {
  ticker: string;
  periods: FinancialPeriod[];
  fetchedAt: string;
  provider: string;
};
