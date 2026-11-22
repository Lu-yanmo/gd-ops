// 模态：标题 + 内嵌 HTML（模板为内部常量，仍过白名单 sanitizer 兜底）。
import { $, sanitize } from './util';

export function openModal(title: string, html: string): void {
  $('#mTitle').textContent = title;
  $('#mBody').innerHTML = sanitize(html);
  $('#modal').classList.remove('assist');
  $('#modal').classList.add('open');
}
export function closeModal(): void {
  $('#modal').classList.remove('open');
  $('#modal').classList.remove('assist');
}
export function wireModal(): void {
  $('#mClose').addEventListener('click', closeModal);
  $('#modal').addEventListener('click', (e) => {
    if (e.target === $('#modal')) closeModal();
  });
}