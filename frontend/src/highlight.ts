import hljs from "highlight.js/lib/core";
import java from "highlight.js/lib/languages/java";

// Register only the languages the skills support to keep the bundle small.
hljs.registerLanguage("java", java);

const extensionToLanguage: Record<string, string> = {
  java: "java",
};

export function languageFor(path: string): string | null {
  const dot = path.lastIndexOf(".");
  if (dot < 0) return null;
  const ext = path.slice(dot + 1).toLowerCase();
  const lang = extensionToLanguage[ext];
  return lang && hljs.getLanguage(lang) ? lang : null;
}

function escapeHtml(text: string): string {
  return text.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
}

/**
 * Highlights a single line for use with dangerouslySetInnerHTML. Line-by-line
 * highlighting keeps the diff layout simple; the tradeoff is that constructs
 * spanning lines (e.g. the middle of a javadoc block) lose their coloring.
 */
export function highlightLine(line: string, language: string | null): { __html: string } {
  if (!language || !line) return { __html: escapeHtml(line) };
  try {
    return { __html: hljs.highlight(line, { language, ignoreIllegals: true }).value };
  } catch {
    return { __html: escapeHtml(line) };
  }
}
