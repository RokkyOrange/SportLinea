(function () {
    const BALANCE_ATTR = 'data-casino-balance';

    function formatBalance(balance) {
        return Number(balance).toLocaleString('ru-RU', { minimumFractionDigits: 2 }) + ' ₽';
    }

    function parseBalance(text) {
        const normalized = String(text || '')
            .replace(/\u00a0/g, '')
            .replace(/\s/g, '')
            .replace('₽', '')
            .replace(',', '.');
        return parseFloat(normalized) || 0;
    }

    window.SportLineaBalance = {
        format: formatBalance,
        parse: parseBalance,
        read() {
            const el = document.querySelector(`[${BALANCE_ATTR}]`);
            return el ? parseBalance(el.textContent) : 0;
        },
        sync(balance, source) {
            document.querySelectorAll(`[${BALANCE_ATTR}]`).forEach((el) => {
                el.textContent = formatBalance(balance);
            });
            window.dispatchEvent(new CustomEvent('sportlinea:balance', {
                detail: { balance: Number(balance), source: source || null }
            }));
        }
    };
})();
