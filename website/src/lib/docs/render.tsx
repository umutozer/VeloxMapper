import "server-only";
import type { ComponentProps, ReactNode } from "react";
import { Fragment, jsx, jsxs } from "react/jsx-runtime";
import { toJsxRuntime, type Components } from "hast-util-to-jsx-runtime";
import type { Root } from "hast";
import Link from "next/link";
import CodeBlock from "@/components/docs/CodeBlock";
import CodeTabs from "@/components/docs/CodeTabs";
import Callout from "@/components/docs/Callout";

type AnyProps = Record<string, unknown> & { children?: ReactNode };

function DocAnchor({ href = "", children, ...rest }: ComponentProps<"a">) {
  if (href.startsWith("/") && !href.startsWith("//")) {
    return (
      <Link href={href} {...rest}>
        {children}
      </Link>
    );
  }
  return (
    <a href={href} {...rest}>
      {children}
    </a>
  );
}

function DocTable(props: ComponentProps<"table">) {
  return (
    <div className="table-wrap" role="region" aria-label="Tablo" tabIndex={0}>
      <table {...props} />
    </div>
  );
}

const components = {
  a: DocAnchor,
  table: DocTable,
  "code-block": (p: AnyProps) => (
    <CodeBlock
      lang={String(p["data-lang"] ?? "text")}
      label={String(p["data-label"] ?? "")}
      title={String(p["data-title"] ?? "")}
      inTabs={p["data-in-tabs"] === "true"}
    >
      {p.children}
    </CodeBlock>
  ),
  "code-tabs": (p: AnyProps) => {
    let tabs: string[] = [];
    try {
      tabs = JSON.parse(String(p["data-tabs"] ?? "[]"));
    } catch {
      tabs = [];
    }
    return <CodeTabs tabs={tabs}>{p.children}</CodeTabs>;
  },
  "doc-callout": (p: AnyProps) => (
    <Callout type={String(p["data-type"] ?? "note")} title={String(p["data-title"] ?? "")}>
      {p.children}
    </Callout>
  ),
} as unknown as Partial<Components>;

/** hast ağacını React öğelerine çevirir (sunucu bileşeni). */
export function renderHast(tree: Root): ReactNode {
  return toJsxRuntime(tree, { Fragment, jsx, jsxs, components, passKeys: true });
}
