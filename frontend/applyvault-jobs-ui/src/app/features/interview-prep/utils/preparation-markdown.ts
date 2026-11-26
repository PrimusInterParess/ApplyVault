import { marked, Renderer } from 'marked';

export function renderPreparationMarkdown(text: string): string {
  const renderer = new Renderer();
  renderer.html = () => '';
  renderer.image = ({ text }) => text.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
  return marked.parse(text, { async: false, renderer });
}
