import { h, s } from "./dom.js";

export function kpi(label, value, { hint, tone } = {}) {
  return h("div", { class: `kpi${tone ? ` kpi--${tone}` : ""}` },
    h("span", { class: "kpi__label" }, label),
    h("strong", { class: "kpi__value" }, value),
    hint ? h("span", { class: "kpi__hint" }, hint) : null);
}

export function notice(kind, title, text) {
  return h("div", { class: `notice notice--${kind}`, role: kind === "error" ? "alert" : "status" },
    h("strong", {}, title),
    text ? h("p", {}, text) : null);
}

export function loading(text = "Lädt …") {
  return h("div", { class: "loading", role: "status" }, h("span", { class: "spinner", "aria-hidden": "true" }), text);
}

export function empty(title, text) {
  return h("div", { class: "empty" }, h("strong", {}, title), h("p", {}, text));
}

export function card(title, subtitle, ...content) {
  return h("section", { class: "card" },
    h("header", { class: "card__header" },
      h("h2", {}, title),
      subtitle ? h("p", {}, subtitle) : null),
    ...content);
}

export function table(columns, rows) {
  return h("div", { class: "table-wrap" },
    h("table", {},
      h("thead", {}, h("tr", {}, columns.map((column) => h("th", { class: column.numeric ? "num" : null, scope: "col" }, column.label)))),
      h("tbody", {}, rows.map((row) =>
        h("tr", { class: row.muted ? "muted" : null }, columns.map((column) =>
          h("td", { class: column.numeric ? "num" : null }, column.render(row))))))));
}

export function select(label, options, value, onChange) {
  const id = `select-${label.replace(/\W+/g, "-").toLowerCase()}`;
  return h("label", { class: "field", for: id },
    h("span", {}, label),
    h("select", { id, onChange: (event) => onChange(event.target.value) },
      options.map((option) => h("option", { value: option.value, selected: String(option.value) === String(value) }, option.label))));
}

export function dateInput(label, value, onChange) {
  const id = `date-${label.replace(/\W+/g, "-").toLowerCase()}`;
  return h("label", { class: "field", for: id },
    h("span", {}, label),
    h("input", { id, type: "date", value, onChange: (event) => onChange(event.target.value) }));
}

// Gruppierte Balken als SVG. series: [{ name, className, values: number[] }]
export function barChart({ labels, series, format, ariaLabel, labelEvery = 1 }) {
  const width = 720;
  const height = 260;
  const margin = { top: 12, right: 8, bottom: 28, left: 56 };
  const plotWidth = width - margin.left - margin.right;
  const plotHeight = height - margin.top - margin.bottom;

  const max = Math.max(0, ...series.flatMap((serie) => serie.values));
  const top = niceMax(max);
  const ticks = [0, top / 4, top / 2, (top * 3) / 4, top];

  const groupWidth = plotWidth / labels.length;
  const barGap = Math.min(4, groupWidth * 0.08);
  const barWidth = Math.max(1, (groupWidth * 0.72 - barGap * (series.length - 1)) / series.length);
  const y = (value) => margin.top + plotHeight - (top === 0 ? 0 : (value / top) * plotHeight);

  const svg = s("svg", { viewBox: `0 0 ${width} ${height}`, class: "chart", role: "img", "aria-label": ariaLabel },
    ticks.map((tick) => [
      s("line", { x1: margin.left, x2: width - margin.right, y1: y(tick), y2: y(tick), class: "chart__grid" }),
      s("text", { x: margin.left - 8, y: y(tick) + 4, class: "chart__tick", "text-anchor": "end" }, format(tick))
    ]),
    labels.map((label, index) => {
      const groupX = margin.left + index * groupWidth + groupWidth * 0.14;
      return s("g", {},
        series.map((serie, serieIndex) => {
          const value = serie.values[index];
          const x = groupX + serieIndex * (barWidth + barGap);
          const barHeight = Math.max(value > 0 ? 1.5 : 0, margin.top + plotHeight - y(value));
          return s("rect", {
            x, y: margin.top + plotHeight - barHeight, width: barWidth, height: barHeight, rx: Math.min(3, barWidth / 2),
            class: `chart__bar ${serie.className}`
          }, s("title", {}, `${label} · ${serie.name}: ${format(value)}`));
        }),
        index % labelEvery === 0
          ? s("text", { x: margin.left + index * groupWidth + groupWidth / 2, y: height - 8, class: "chart__label", "text-anchor": "middle" }, label)
          : null);
    }));

  const legend = h("ul", { class: "legend" },
    series.map((serie) => h("li", {}, h("span", { class: `legend__swatch ${serie.className}`, "aria-hidden": "true" }), serie.name)));

  return h("figure", { class: "chart-figure" }, h("div", { class: "chart-scroll" }, svg), legend);
}

function niceMax(value) {
  if (value <= 0) {
    return 1;
  }
  const exponent = 10 ** Math.floor(Math.log10(value));
  const fraction = value / exponent;
  const nice = fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 2.5 ? 2.5 : fraction <= 5 ? 5 : 10;
  return nice * exponent;
}
