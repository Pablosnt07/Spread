export type CompanyProfile = {
  ticker: string;
  companyName: string;
  assetType: string;
  sector: string | null;
  industry: string | null;
  exchange: string | null;
  currency: string | null;
  country: string | null;
  marketCapitalization: number | null;
  beta: number | null;
  isActivelyTrading: boolean;
  website: string | null;
  logoUrl: string | null;
  fetchedAt: string;
  provider: string;
};
