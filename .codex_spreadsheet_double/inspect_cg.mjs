import { FileBlob, SpreadsheetFile } from "@oai/artifact-tool";

const input = await FileBlob.load("C:/MAGLA/CG сравнение.xlsm");
const workbook = await SpreadsheetFile.importXlsx(input);

const summary = await workbook.inspect({
  kind: "workbook,sheet,table,drawing",
  maxChars: 20000,
  tableMaxRows: 12,
  tableMaxCols: 12,
  tableMaxCellChars: 120,
});
console.log(summary.ndjson);

const formulas = await workbook.inspect({
  kind: "formula",
  range: "A1:AZ200",
  maxChars: 25000,
  options: { maxResults: 300 },
});
console.log(formulas.ndjson);

const comparison = await workbook.inspect({
  kind: "table",
  range: "расчет!P28:AF40",
  include: "values,formulas",
  tableMaxRows: 20,
  tableMaxCols: 20,
  maxChars: 16000,
});
console.log(comparison.ndjson);

const methodLabels = await workbook.inspect({
  kind: "match",
  searchTerm: "CG|PCG|BICGSTAB|невязк|итерац|положительн",
  options: { useRegex: true, maxResults: 250 },
  maxChars: 18000,
});
console.log(methodLabels.ndjson);

const pcg = await workbook.inspect({
  kind: "table",
  range: "'IC(0)+PCG'!A1:AL89",
  include: "values,formulas",
  tableMaxRows: 89,
  tableMaxCols: 38,
  tableMaxCellChars: 100,
  maxChars: 30000,
});
console.log(pcg.ndjson);
