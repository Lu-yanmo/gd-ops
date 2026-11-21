// 小工具 + 事件总线：state 发事件、render 订阅，避免模块循环引用。
export function $<T extends Element = HTMLElement>(sel: string): T {
  const el = document.querySelector(sel);
  if (!el) throw new Error('DOM missing: ' + sel);
  return el as T;
}
export const pad = (n: number): string => (n < 10 ? '0' : '') + n;
export const rnd = (a: number, b: number): number => a + Math.random() * (b - a);
export function now(): string {
  const d = new Date();
  return pad(d.getHours()) + ':' + pad(d.getMinutes()) + ':' + pad(d.getSeconds());
}
export function dstr(): string {
  const d = new Date();
  return d.getFullYear() + '-' + pad(d.getMonth() + 1) + '-' + pad(d.getDate());
}
export const REDUCED = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

// 年龄读数（只读快照的 ageSecs）：s/m/h/d 四档。
export function humanAge(sec: number): string {
  if (!Number.isFinite(sec) || sec < 0) return '—';
  if (sec < 90) return Math.round(sec) + 's';
  if (sec < 5400) return Math.round(sec / 60) + 'm';
  if (sec < 172800) return Math.round(sec / 3600) + 'h';
  return Math.round(sec / 86400) + 'd';
}

// 数值/状态变更的一次性微闪（WAAPI，只动 opacity；reduced-motion 下静默跳过）。
export function blip(el: Element | null, from = 0.35): void {
  if (!el || REDUCED) return;
  (el as HTMLElement).animate([{ opacity: from }, { opacity: 1 }], { duration: 260, easing: 'cubic-bezier(.2,.9,.3,1)' });
}

// 动态串进 HTML 前一律转义（日志 msg 等）。
export function esc(s: string): string {
  return s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

// 模态内嵌 HTML 的白名单清洗：模板虽为内部常量，仍过一道 DOM 级 sanitizer，
// 去掉 script/iframe 等标签、on* 属性与 javascript: URL，只留版式所需标签/属性。
const ALLOWED_TAGS = new Set(['DIV', 'SPAN', 'CODE', 'B', 'BR', 'BUTTON', 'INPUT', 'I', 'P', 'UL', 'LI']);
export function sanitize(html: string): string {
  const doc = new DOMParser().parseFromString(html, 'text/html');
  const walk = (node: Element): void => {
    for (const el of Array.from(node.children)) {
      if (!ALLOWED_TAGS.has(el.tagName)) { el.remove(); continue; }
      for (const attr of Array.from(el.attributes)) {
        const n = attr.name.toLowerCase();
        const allowed = n === 'class' || n === 'style' || n === 'type' || n === 'value' || n === 'id' || n.startsWith('data-');
        if (!allowed || n.startsWith('on')) { el.removeAttribute(attr.name); continue; }
        if (/^\s*javascript:/i.test(attr.value)) el.removeAttribute(attr.name);
      }
      walk(el);
    }
  };
  walk(doc.body);
  return doc.body.innerHTML;
}

type Handler = () => void;
const listeners: Record<string, Handler[]> = {};
export function on(evt: string, fn: Handler): void {
  (listeners[evt] = listeners[evt] || []).push(fn);
}
export function emit(evt: string): void {
  (listeners[evt] || []).forEach((f) => f());
}