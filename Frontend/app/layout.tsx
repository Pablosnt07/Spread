import type { Metadata } from "next";
import { GeistMono } from "geist/font/mono";
import { GeistSans } from "geist/font/sans";
import { ScrollMotion } from "@/components/ScrollMotion";
import "./globals.css";

export const metadata: Metadata = {
  title: "Spread — Investment intelligence",
  description: "Análisis fundamental claro, comparable y explicable.",
};

export default function RootLayout({ children }: Readonly<{ children: React.ReactNode }>) {
  return (
    <html lang="es" data-scroll-behavior="smooth" className={`${GeistSans.variable} ${GeistMono.variable}`}>
      <body><ScrollMotion />{children}</body>
    </html>
  );
}
