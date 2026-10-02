// DOM ohne innerHTML: Texte gehen immer über textContent, damit Daten aus der API nie als HTML
// interpretiert werden.

const SVG = "http://www.w3.org/2000/svg";

export function h(tag, attributes = {}, ...children) {
  const element = document.createElement(tag);
  applyAttributes(element, attributes);
  append(element, children);
  return element;
}

export function s(tag, attributes = {}, ...children) {
  const element = document.createElementNS(SVG, tag);
  for (const [name, value] of Object.entries(attributes)) {
    if (value !== undefined && value !== null && value !== false) {
      element.setAttribute(name, String(value));
    }
  }
  append(element, children);
  return element;
}

function applyAttributes(element, attributes) {
  for (const [name, value] of Object.entries(attributes)) {
    if (value === undefined || value === null || value === false) {
      continue;
    }
    if (name.startsWith("on") && typeof value === "function") {
      element.addEventListener(name.slice(2).toLowerCase(), value);
    } else if (name === "dataset") {
      Object.assign(element.dataset, value);
    } else if (value === true) {
      element.setAttribute(name, "");
    } else {
      element.setAttribute(name, String(value));
    }
  }
}

function append(element, children) {
  for (const child of children.flat(Infinity)) {
    if (child === null || child === undefined || child === false) {
      continue;
    }
    element.append(child instanceof Node ? child : document.createTextNode(String(child)));
  }
}

export function replace(container, ...children) {
  container.replaceChildren();
  append(container, children);
}
