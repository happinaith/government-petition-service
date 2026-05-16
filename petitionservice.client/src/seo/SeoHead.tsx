import { useEffect } from "react";

interface SeoHeadProps {
  title: string;
  description: string;
  canonicalPath?: string;
  imagePath?: string;
  robots?: string;
  type?: "website" | "article";
  structuredData?: Record<string, unknown>;
}

function upsertMeta(selector: string, attributes: Record<string, string>, content: string): void {
  let element = document.head.querySelector<HTMLMetaElement>(selector);
  if (!element) {
    element = document.createElement("meta");
    Object.entries(attributes).forEach(([key, value]) => element?.setAttribute(key, value));
    document.head.appendChild(element);
  }

  element.setAttribute("content", content);
}

function upsertCanonical(url: string): void {
  let link = document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');
  if (!link) {
    link = document.createElement("link");
    link.setAttribute("rel", "canonical");
    document.head.appendChild(link);
  }

  link.setAttribute("href", url);
}

export function SeoHead({
  title,
  description,
  canonicalPath,
  imagePath = "/vite.svg",
  robots = "index, follow",
  type = "website",
  structuredData,
}: SeoHeadProps): null {
  useEffect(() => {
    const origin = window.location.origin;
    const canonicalUrl = new URL(canonicalPath ?? window.location.pathname, origin).toString();
    const imageUrl = new URL(imagePath, origin).toString();

    document.title = title;
    upsertMeta('meta[name="description"]', { name: "description" }, description);
    upsertMeta('meta[name="robots"]', { name: "robots" }, robots);

    upsertMeta('meta[property="og:title"]', { property: "og:title" }, title);
    upsertMeta('meta[property="og:description"]', { property: "og:description" }, description);
    upsertMeta('meta[property="og:type"]', { property: "og:type" }, type);
    upsertMeta('meta[property="og:url"]', { property: "og:url" }, canonicalUrl);
    upsertMeta('meta[property="og:image"]', { property: "og:image" }, imageUrl);

    upsertMeta('meta[name="twitter:card"]', { name: "twitter:card" }, "summary_large_image");
    upsertMeta('meta[name="twitter:title"]', { name: "twitter:title" }, title);
    upsertMeta('meta[name="twitter:description"]', { name: "twitter:description" }, description);
    upsertMeta('meta[name="twitter:image"]', { name: "twitter:image" }, imageUrl);

    upsertCanonical(canonicalUrl);

    const existingJsonLd = document.head.querySelector<HTMLScriptElement>('script[type="application/ld+json"][data-seo="json-ld"]');
    if (structuredData) {
      const jsonLdScript = existingJsonLd ?? document.createElement("script");
      jsonLdScript.setAttribute("type", "application/ld+json");
      jsonLdScript.setAttribute("data-seo", "json-ld");
      jsonLdScript.textContent = JSON.stringify(structuredData);
      if (!existingJsonLd) {
        document.head.appendChild(jsonLdScript);
      }
    } else if (existingJsonLd) {
      existingJsonLd.remove();
    }
  }, [canonicalPath, description, imagePath, robots, structuredData, title, type]);

  return null;
}
