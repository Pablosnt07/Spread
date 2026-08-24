"use client";

import { useEffect } from "react";

const motionTargets = [
  ".trends .section-heading",
  ".trend-table",
  ".analysis-shell > .company-summary",
  ".analysis-shell > section",
  ".workspace-title",
  ".workspace-tabs",
  ".portfolio-layout > section",
  ".watchlist-panel",
  ".compare-panel",
].join(",");

export function ScrollMotion() {
  useEffect(() => {
    if (window.matchMedia("(prefers-reduced-motion: reduce)").matches) return;

    const registered = new WeakSet<Element>();
    const observer = new IntersectionObserver((entries) => {
      entries.forEach((entry) => {
        if (!entry.isIntersecting) return;

        const element = entry.target as HTMLElement;
        const isWipe = element.matches(".trend-table, .holdings-panel, .insider-section, .raw-data");
        const isFocus = element.matches(".signature-section, .evidence-section, .portfolio-overview");

        element.animate(
          isWipe
            ? [
                { opacity: 0.62, clipPath: "inset(0 7% 0 0)", transform: "translateX(-12px)" },
                { opacity: 1, clipPath: "inset(0 0 0 0)", transform: "translateX(0)" },
              ]
            : isFocus
              ? [
                  { opacity: 0.68, filter: "blur(5px)", transform: "scale(0.985)" },
                  { opacity: 1, filter: "blur(0)", transform: "scale(1)" },
                ]
              : [
                  { opacity: 0.72, transform: "translateY(18px)" },
                  { opacity: 1, transform: "translateY(0)" },
                ],
          { duration: isFocus ? 520 : 420, easing: "cubic-bezier(0.16, 1, 0.3, 1)", fill: "none" },
        );

        observer.unobserve(element);
      });
    }, { rootMargin: "0px 0px -12%", threshold: 0.08 });

    const observeElement = (element: Element) => {
      if (registered.has(element)) return;
      registered.add(element);
      observer.observe(element);
    };
    const register = (root: ParentNode) => {
      if (root instanceof Element && root.matches(motionTargets)) observeElement(root);
      root.querySelectorAll(motionTargets).forEach(observeElement);
    };

    register(document);
    const mutations = new MutationObserver((records) => {
      records.forEach((record) => record.addedNodes.forEach((node) => {
        if (node instanceof Element) register(node);
      }));
    });
    mutations.observe(document.body, { childList: true, subtree: true });

    return () => {
      mutations.disconnect();
      observer.disconnect();
    };
  }, []);

  return null;
}
