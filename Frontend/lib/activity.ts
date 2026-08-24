export type InsiderCategory = "Purchase" | "Sale" | "Award" | "Exercise" | "Gift" | "Other";

export type InsiderTransaction = {
  filingDate: string;
  transactionDate: string | null;
  reportingName: string;
  ownerType: string | null;
  transactionType: string | null;
  acquisitionOrDisposition: string | null;
  category: InsiderCategory;
  securitiesTransacted: number | null;
  price: number | null;
  transactionValue: number | null;
  securitiesOwned: number | null;
  securityName: string | null;
  source: string;
  filingUrl: string | null;
};

export type DividendEvent = {
  exDividendDate: string;
  declarationDate: string | null;
  recordDate: string | null;
  paymentDate: string | null;
  dividend: number | null;
  adjustedDividend: number | null;
  yield: number | null;
  frequency: string | null;
};

export type CompanyMarketActivity = {
  ticker: string;
  insiderTransactions: InsiderTransaction[];
  dividends: DividendEvent[];
  insiderDataAvailable: boolean;
  dividendDataAvailable: boolean;
  fetchedAt: string;
  provider: string;
};
