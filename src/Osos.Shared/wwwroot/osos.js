// Tarayıcıda dosya indirme (Blazor WASM + MAUI BlazorWebView).
window.ososDownload = (fileName, base64, mime) => {
    const a = document.createElement('a');
    a.href = `data:${mime};base64,${base64}`;
    a.download = fileName;
    document.body.appendChild(a);
    a.click();
    a.remove();
};
