window.printJournal = function (bodyHtml) {
    var w = window.open('', '_blank');
    w.document.write('<!DOCTYPE html><html><head><meta charset="utf-8"><style>' +
        '* { box-sizing: border-box; margin: 0; padding: 0; }' +
        'body { font-family: Arial, Helvetica, sans-serif; font-size: 11px; color: #000; background: #fff; padding: 18px; }' +
        'h1 { font-size: 15px; font-weight: bold; margin-bottom: 3px; }' +
        '.sub { font-size: 10px; color: #555; margin-bottom: 14px; }' +
        'table { border-collapse: collapse; width: 100%; }' +
        'th { background: #e8e8e8; font-size: 9.5px; font-weight: bold; text-transform: uppercase;' +
             'letter-spacing: 0.04em; padding: 5px 3px; border: 1px solid #999; text-align: center; }' +
        'th.nc { text-align: left; padding-left: 7px; min-width: 150px; }' +
        'td { border: 1px solid #ccc; padding: 4px 3px; text-align: center; font-size: 11px; }' +
        'td.nc { text-align: left; padding-left: 7px; }' +
        '.d { font-weight: 700; }' +
        '.r { font-style: italic; color: #444; }' +
        '.rej { color: #888; text-decoration: line-through; }' +
        '.lo { color: #bbb; }' +
        '.av { font-weight: 700; font-size: 12px; }' +
        'tr:nth-child(even) td { background: #f7f7f7; }' +
        '.leg { margin-top: 12px; font-size: 9.5px; color: #666; line-height: 1.6; }' +
        '@media print {' +
        '  @page { margin: 12mm; size: A4 landscape; }' +
        '  body { padding: 0; }' +
        '}' +
        '</style></head><body>' +
        bodyHtml +
        '</body></html>');
    w.document.close();
    w.focus();
    setTimeout(function () { w.print(); }, 350);
};
