import fs from "node:fs/promises";
import { SpreadsheetFile, Workbook } from "@oai/artifact-tool";

const outputDir = "C:/Users/Georgy/.codex/visualizations/2026/08/17/01a00e59-704c-7e50-a013-8f463d6ab5e6/outputs/double_number";
await fs.mkdir(outputDir, { recursive: true });

const workbook = Workbook.create();
const sheet = workbook.worksheets.add("Расчёт");
sheet.showGridLines = false;

sheet.getRange("A1:D1").merge();
sheet.getRange("A1").values = [["Умножение числа на 2"]];
sheet.getRange("A1:D1").format = {
  fill: "#1F4E78",
  font: { bold: true, color: "#FFFFFF", size: 16 },
  horizontalAlignment: "center",
  verticalAlignment: "center",
};
sheet.getRange("A1:D1").format.rowHeight = 30;

sheet.getRange("A3:D3").values = [[
  "Введите число",
  "Результат",
  null,
  "Множитель",
]];
sheet.getRange("A3:B3").format = {
  fill: "#D9EAF7",
  font: { bold: true, color: "#1F1F1F" },
  horizontalAlignment: "center",
  borders: { preset: "outside", style: "thin", color: "#9EADBA" },
};
sheet.getRange("D3").format = {
  fill: "#E7E6E6",
  font: { bold: true, color: "#404040" },
  horizontalAlignment: "center",
};

sheet.getRange("A4").values = [[5]];
sheet.getRange("B4").formulas = [["=A4*$D$4"]];
sheet.getRange("D4").values = [[2]];

sheet.getRange("A4").format = {
  fill: "#FFF2CC",
  font: { bold: true, color: "#7F6000", size: 14 },
  horizontalAlignment: "center",
  numberFormat: "0.00",
  borders: { preset: "outside", style: "medium", color: "#BF9000" },
};
sheet.getRange("B4").format = {
  fill: "#E2F0D9",
  font: { bold: true, color: "#375623", size: 14 },
  horizontalAlignment: "center",
  numberFormat: "0.00",
  borders: { preset: "outside", style: "medium", color: "#70AD47" },
};
sheet.getRange("D4").format = {
  fill: "#F2F2F2",
  horizontalAlignment: "center",
  numberFormat: "0",
};

sheet.getRange("A6:D6").merge();
sheet.getRange("A6").values = [[
  "Измените значение в жёлтой ячейке — результат пересчитается автоматически.",
]];
sheet.getRange("A6:D6").format = {
  font: { italic: true, color: "#666666" },
  horizontalAlignment: "left",
  wrapText: true,
};

sheet.getRange("A1:D6").format.verticalAlignment = "center";
sheet.getRange("A:A").format.columnWidth = 20;
sheet.getRange("B:B").format.columnWidth = 18;
sheet.getRange("C:C").format.columnWidth = 4;
sheet.getRange("D:D").format.columnWidth = 14;
sheet.getRange("A3:D4").format.rowHeight = 24;
sheet.getRange("A6:D6").format.rowHeight = 34;

const check = await workbook.inspect({
  kind: "table",
  range: "Расчёт!A1:D6",
  include: "values,formulas",
  tableMaxRows: 10,
  tableMaxCols: 6,
});
console.log(check.ndjson);

const errors = await workbook.inspect({
  kind: "match",
  searchTerm: "#REF!|#DIV/0!|#VALUE!|#NAME\\?|#N/A",
  options: { useRegex: true, maxResults: 100 },
  summary: "final formula error scan",
});
console.log(errors.ndjson);

const preview = await workbook.render({
  sheetName: "Расчёт",
  range: "A1:D6",
  scale: 2,
  format: "png",
});
await fs.writeFile(
  `${outputDir}/preview.png`,
  new Uint8Array(await preview.arrayBuffer()),
);

const output = await SpreadsheetFile.exportXlsx(workbook);
await output.save(`${outputDir}/умножение_на_2.xlsx`);
