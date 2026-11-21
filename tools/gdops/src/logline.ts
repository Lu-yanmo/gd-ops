// 日志行解析：两种真实形态——落盘日志三括号 [日期 时间][时间][LEVEL]，
// pwsh 控制台回传两括号 [时间][LEVEL]（日期用今天补）。
import type { Level } from './types';
import { dstr, esc } from './util';

const LEVELS = ['INFO', 'STEP', 'OK', 'WARN', 'ERROR'];

export function parseLine(raw: string): { lv: Level; html: string } {
  const m3 = raw.match(/^\[([^\]]+)\]\[([^\]]+)\]\[([A-Z]+)\]\s?([\s\S]*)$/);
  if (m3) {
    const lv = (LEVELS.indexOf(m3[3]) >= 0 ? m3[3] : 'INFO') as Level;
    return { lv, html: '<span class="t">[' + esc(m3[1]) + '][' + esc(m3[2]) + ']</span><span class="lv ' + lv + '">' + lv + '</span><span class="msg">' + esc(m3[4]) + '</span>' };
  }
  const m2 = raw.match(/^\[([^\]]+)\]\[([A-Z]+)\]\s?([\s\S]*)$/);
  if (m2) {
    const lv = (LEVELS.indexOf(m2[2]) >= 0 ? m2[2] : 'INFO') as Level;
    return { lv, html: '<span class="t">[' + esc(dstr() + ' ' + m2[1]) + '][' + esc(m2[1]) + ']</span><span class="lv ' + lv + '">' + lv + '</span><span class="msg">' + esc(m2[3]) + '</span>' };
  }
  return { lv: 'INFO', html: lineHtml('INFO', raw) };
}

export function lineHtml(lv: Level, msg: string): string {
  return '<span class="t"></span><span class="lv ' + lv + '">' + lv + '</span><span class="msg">' + esc(msg) + '</span>';
}
