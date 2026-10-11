export const $ = (id) => document.getElementById(id);

let noticeTimer;

export function notice(message, error = false) {
  clearTimeout(noticeTimer);
  $("notice").textContent = message;
  $("notice").hidden = false;
  $("notice").classList.toggle("error", error);
  if (!error)
    noticeTimer = setTimeout(() => {
      $("notice").hidden = true;
    }, 6000);
}

export function el(tag, cls, text) {
  const node = document.createElement(tag);
  if (cls) node.className = cls;
  if (text !== undefined) node.textContent = text;
  return node;
}
