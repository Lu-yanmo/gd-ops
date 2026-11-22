// 左侧二级框：浮在左列（游戏路/任务队列）右边，盖在中列上方——
// 打开时 console 栅格一列不动（不挤压中列/右列），抽屉是绝对定位的悬浮层。
// Esc / × / 取消 关闭；同刻只开一个。
import { esc } from './util';

export interface DrawerHandle {
  root: HTMLElement;
  body: HTMLElement;
  foot: HTMLElement;
  close: () => void;
}

let live: { pane: HTMLElement; timer: number; onKey: (e: KeyboardEvent) => void; onDoc: (e: MouseEvent) => void; close: () => void } | null = null;
let liveKey = '';

export function drawerOpen(): boolean {
  return !!live;
}

/** 当前抽屉的归属键（`maa:<idx>` / `m9a:<id>`）：齿轮据此做「同一条再点=收」。 */
export function drawerKey(): string {
  return liveKey;
}

export function closeDrawer(): void {
  if (live) live.close();
}

export function openDrawer(title: string, sub: string, bodyHtml: string, footHtml: string, key = ''): DrawerHandle {
  closeNow();
  liveKey = key;
  const prevFocus = document.activeElement as HTMLElement | null;

  // 宿主：.console 里绝对定位的浮层（左列右边）；栅格列数不变，中列/右列不挤压。
  const consoleEl = document.querySelector('.console') as HTMLElement | null;
  const pane = document.createElement('section');
  pane.className = 'panel drPanel';
  pane.setAttribute('role', 'dialog');
  pane.setAttribute('aria-modal', 'false');
  pane.innerHTML =
    '<div class="panel-h"><span class="cn">' + esc(title) + '</span>' +
    (sub ? '<span class="en">' + esc(sub) + '</span>' : '') +
    '<button class="drX" type="button" title="关闭（Esc）" aria-label="关闭">×</button></div>' +
    '<div class="drBody" tabindex="-1">' + bodyHtml + '</div>' +
    '<div class="drFoot">' + footHtml + '</div>';

  if (consoleEl) {
    consoleEl.appendChild(pane);
  } else {
    document.body.appendChild(pane);
  }

  const body = pane.querySelector('.drBody') as HTMLElement;
  const foot = pane.querySelector('.drFoot') as HTMLElement;

  const onKey = (e: KeyboardEvent) => {
    if (e.key === 'Escape') {
      e.preventDefault();
      close();
    }
  };
  document.addEventListener('keydown', onKey);

  // 点外面自动收回；齿轮本身除外——同一条齿轮再点由队列侧判「收」，另一条则换开。
  const onDoc = (e: MouseEvent) => {
    const t = e.target as Node | null;
    if (!t || pane.contains(t)) return;
    if (t instanceof Element && t.closest('[data-g]')) return;
    close();
  };
  document.addEventListener('click', onDoc);

  (pane.querySelector('.drX') as HTMLElement).addEventListener('click', () => close());

  window.requestAnimationFrame(() => pane.classList.add('on'));

  const timer = window.setTimeout(() => {
    if (!body.contains(document.activeElement)) {
      const first = body.querySelector<HTMLElement>('input,select,textarea,button');
      (first || body).focus({ preventScroll: true });
    }
  }, 60);

  const close = () => {
    if (!pane.isConnected) return;
    window.clearTimeout(timer);
    pane.classList.remove('on');
    document.removeEventListener('keydown', onKey);
    document.removeEventListener('click', onDoc);
    live = null;
    window.setTimeout(() => {
      pane.remove();
      // 只有当焦点本来在抽屉里（或已经落空）才还回去；用户点了别处就留在别处。
      const a = document.activeElement;
      if (prevFocus && prevFocus.isConnected && (!a || a === document.body || pane.contains(a))) {
        prevFocus.focus({ preventScroll: true });
      }
    }, 240);
  };

  live = { pane, timer, onKey, onDoc, close };

  return { root: pane, body, foot, close };
}

function closeNow(): void {
  if (!live) return;
  const { pane, timer, onKey, onDoc } = live;
  window.clearTimeout(timer);
  document.removeEventListener('keydown', onKey);
  document.removeEventListener('click', onDoc);
  pane.remove();
  live = null;
  liveKey = '';
}