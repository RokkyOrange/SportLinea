document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('fruitlandForm');
    if (!form) return;

    const spinBtn = document.getElementById('fruitlandSpinBtn');
    const buy10Btn = document.getElementById('fruitlandBuy10Btn');
    const buy15Btn = document.getElementById('fruitlandBuy15Btn');
    const buy20Btn = document.getElementById('fruitlandBuy20Btn');
    const balanceEl = document.getElementById('fruitlandBalance');
    const amountInput = document.getElementById('fruitlandAmount');
    const stakeChips = [...document.querySelectorAll('.fruitland-stake-chip')];
    const resultBox = document.getElementById('fruitlandResult');
    const buyHints = document.getElementById('fruitlandBuyHints');
    const bonusBanner = document.getElementById('fruitlandBonusBanner');
    const globalMultEl = document.getElementById('fruitlandGlobalMult');
    const freeSpinInfo = document.getElementById('fruitlandFreeSpinInfo');
    const cells = [...document.querySelectorAll('.fruitland-cell')];

    const spinUrl = form.dataset.spinUrl;
    const buyUrl = form.dataset.buyUrl;
    const completeUrl = form.dataset.completeUrl;
    const buy10Mult = parseInt(form.dataset.buy10Multiplier || '100', 10);
    const buy15Mult = parseInt(form.dataset.buy15Multiplier || '150', 10);
    const buy20Mult = parseInt(form.dataset.buy20Multiplier || '200', 10);
    const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value;

    const SYMBOL_EMOJI = {
        pear: '🍐', watermelon: '🍉', lemon: '🍋', grape: '🍇',
        cherry: '🍒', peach: '🍑', greenapple: '🍏', strawberry: '🍓',
        lightning: '⚡', scatter: '💎'
    };

    let busy = false;
    const stepDelayMs = 70;
    const highlightDelayMs = 500;
    const winDelayMs = 600;
    const cascadeDelayMs = 400;
    const phasePauseMs = 900;
    const bonusSpinMinDurationMs = 1000;

    let bonusSpinStartedAt = 0;
    let inBonusPhase = false;

    spinBtn?.addEventListener('click', () => { if (!busy) doSpin('spin'); });
    buy10Btn?.addEventListener('click', () => { if (!busy) doSpin('buy10'); });
    buy15Btn?.addEventListener('click', () => { if (!busy) doSpin('buy15'); });
    buy20Btn?.addEventListener('click', () => { if (!busy) doSpin('buy20'); });

    stakeChips.forEach((chip) => {
        chip.addEventListener('click', () => {
            if (chip.disabled || busy) return;
            selectStake(parseInt(chip.dataset.amount, 10));
        });
    });

    updateStakeAvailability();
    updateBuyHints();

    window.addEventListener('sportlinea:balance', (event) => {
        if (event.detail?.source === 'fruitland') return;
        updateStakeAvailability();
        updateBuyHints();
    });

    function getBalance() {
        if (window.SportLineaBalance) return window.SportLineaBalance.read();
        return parseFloat(String(balanceEl?.textContent || '').replace(/\s/g, '').replace(',', '.')) || 0;
    }

    function getSelectedStake() {
        return parseInt(amountInput?.value || '0', 10);
    }

    function selectStake(amount) {
        if (!amountInput) return;
        amountInput.value = amount;
        stakeChips.forEach((chip) => {
            chip.classList.toggle('active', parseInt(chip.dataset.amount, 10) === amount);
        });
        updateBuyHints();
        updateStakeAvailability();
    }

    function updateBuyHints() {
        if (!buyHints) return;
        const stake = getSelectedStake();
        if (stake <= 0) {
            buyHints.textContent = '';
            return;
        }
        buyHints.textContent =
            `10 FS: ${(stake * buy10Mult).toLocaleString('ru-RU')} ₽ · ` +
            `15 FS: ${(stake * buy15Mult).toLocaleString('ru-RU')} ₽ · ` +
            `20 FS: ${(stake * buy20Mult).toLocaleString('ru-RU')} ₽`;
    }

    function updateStakeAvailability() {
        const balance = getBalance();
        let selected = getSelectedStake();
        let highestAffordable = null;

        stakeChips.forEach((chip) => {
            const amount = parseInt(chip.dataset.amount, 10);
            const affordable = amount <= balance;
            chip.disabled = !affordable || busy;
            chip.classList.toggle('disabled', !affordable);
            if (affordable) highestAffordable = amount;
        });

        if (!selected || selected > balance) {
            selected = highestAffordable || parseInt(stakeChips[0]?.dataset.amount || '10', 10);
            if (amountInput) amountInput.value = selected;
            stakeChips.forEach((chip) => {
                chip.classList.toggle('active', parseInt(chip.dataset.amount, 10) === selected);
            });
            updateBuyHints();
        }

        const s = getSelectedStake();
        const spinAffordable = !!highestAffordable;
        setActionBtn(spinBtn, spinAffordable);
        setActionBtn(buy10Btn, s > 0 && balance >= s * buy10Mult);
        setActionBtn(buy15Btn, s > 0 && balance >= s * buy15Mult);
        setActionBtn(buy20Btn, s > 0 && balance >= s * buy20Mult);
    }

    function setActionBtn(btn, affordable) {
        if (!btn) return;
        const canUse = affordable && !busy;
        btn.disabled = !canUse;
        btn.classList.toggle('fruitland-btn-unaffordable', !canUse);
    }

    function updateBalance(balance) {
        if (window.SportLineaBalance) {
            window.SportLineaBalance.sync(balance, 'fruitland');
        } else if (balanceEl) {
            balanceEl.textContent = balance.toLocaleString('ru-RU', { minimumFractionDigits: 2 }) + ' ₽';
        }
        updateStakeAvailability();
    }

    function showResult(text, type) {
        if (!resultBox) return;
        resultBox.style.display = 'block';
        resultBox.className = 'roulette-result mt-3 result-' + type;
        resultBox.textContent = text;
    }

    function pick(obj, camel, pascal) {
        if (!obj) return undefined;
        return obj[camel] ?? obj[pascal];
    }

    function getCell(row, col) {
        const r = Number(row);
        const c = Number(col);
        return cells.find((el) =>
            parseInt(el.dataset.row, 10) === r &&
            parseInt(el.dataset.col, 10) === c);
    }

    function clearGrid() {
        cells.forEach((cell) => {
            cell.className = 'fruitland-cell';
            delete cell.dataset.lightningMult;
            const inner = cell.querySelector('.fruitland-cell-inner');
            if (inner) inner.textContent = '';
        });
    }

    function setGlobalMult(value) {
        if (!globalMultEl) return;
        const v = Number(value) || 0;
        globalMultEl.textContent = `×${v}`;
        globalMultEl.classList.toggle('fruitland-global-mult-active', v > 0);
    }

    function applyLightningTier(el, mult) {
        if (!el) return;
        el.classList.remove('fruitland-lightning-tier-50', 'fruitland-lightning-tier-1000');
        const m = Number(mult) || 0;
        if (m >= 1000) el.classList.add('fruitland-lightning-tier-1000');
        else if (m >= 50) el.classList.add('fruitland-lightning-tier-50');
    }

    function renderGrid(gridCells) {
        clearGrid();
        for (const cell of gridCells || []) {
            const el = getCell(cell.row ?? cell.Row, cell.col ?? cell.Col);
            if (!el) continue;
            const kind = String(pick(cell, 'kind', 'Kind') || '').toLowerCase();
            const emoji = SYMBOL_EMOJI[kind] || '?';
            el.className = 'fruitland-cell fruitland-cell-revealed fruitland-' + kind;
            const inner = el.querySelector('.fruitland-cell-inner');
            if (inner) inner.textContent = emoji;
            if (kind === 'lightning') {
                const mult = Number(cell.multiplier ?? cell.Multiplier ?? 0);
                el.dataset.lightningMult = mult > 0 ? String(mult) : '';
                applyLightningTier(el, mult);
                if (inner) inner.textContent = '⚡';
            }
        }
    }

    function highlightCells(winCells) {
        cells.forEach((c) => {
            c.classList.remove(
                'fruitland-cell-highlight',
                'fruitland-lightning-mult',
                'fruitland-lightning-mult-50',
                'fruitland-lightning-mult-1000');
            const inner = c.querySelector('.fruitland-cell-inner');
            if (inner && c.classList.contains('fruitland-lightning')) {
                inner.textContent = '⚡';
                applyLightningTier(c, c.dataset.lightningMult);
            }
        });
        for (const cell of winCells || []) {
            const el = getCell(cell.row ?? cell.Row, cell.col ?? cell.Col);
            if (el) el.classList.add('fruitland-cell-highlight');
        }
    }

    function revealLightningMultipliers(winCells) {
        for (const cell of winCells || []) {
            const mult = Number(cell.multiplier ?? cell.Multiplier ?? 0);
            if (!mult) continue;
            const el = getCell(cell.row ?? cell.Row, cell.col ?? cell.Col);
            if (!el) continue;
            el.classList.add('fruitland-lightning-mult');
            if (mult >= 1000) el.classList.add('fruitland-lightning-mult-1000');
            else if (mult >= 50) el.classList.add('fruitland-lightning-mult-50');
            const inner = el.querySelector('.fruitland-cell-inner');
            if (inner) inner.textContent = `×${mult}`;
        }
    }

    async function padBonusSpinIfNeeded() {
        if (!inBonusPhase || !bonusSpinStartedAt) return;
        const elapsed = Date.now() - bonusSpinStartedAt;
        if (elapsed < bonusSpinMinDurationMs)
            await sleep(bonusSpinMinDurationMs - elapsed);
        bonusSpinStartedAt = 0;
    }

    function normalizeSteps(phase) {
        const raw = phase.steps ?? phase.Steps ?? [];
        return raw.map((step) => ({
            type: (pick(step, 'type', 'Type') || '').toLowerCase(),
            grid: pick(step, 'grid', 'Grid'),
            win: pick(step, 'win', 'Win'),
            globalMultiplier: pick(step, 'globalMultiplier', 'GlobalMultiplier'),
            freeSpin: pick(step, 'freeSpin', 'FreeSpin'),
            freeSpinsTotal: pick(step, 'freeSpinsTotal', 'FreeSpinsTotal'),
            freeSpinsAdded: pick(step, 'freeSpinsAdded', 'FreeSpinsAdded'),
            scatterCount: pick(step, 'scatterCount', 'ScatterCount')
        }));
    }

    function sleep(ms) {
        return new Promise((resolve) => setTimeout(resolve, ms));
    }

    async function playPhase(phase, balanceRef) {
        const phaseKey = String(phase.phase ?? phase.Phase ?? '').toLowerCase();
        const triggersBonus = !!(phase.triggersBonus ?? phase.TriggersBonus);
        const phaseWinnings = Number(phase.winnings ?? phase.Winnings ?? 0);
        const steps = normalizeSteps(phase);

        if (phaseKey === 'bonus' && bonusBanner)
            bonusBanner.hidden = false;

        inBonusPhase = phaseKey === 'bonus';
        bonusSpinStartedAt = 0;

        for (const step of steps) {
            if (step.type === 'bonusstart') {
                if (bonusBanner) bonusBanner.hidden = false;
                setGlobalMult(0);
                if (freeSpinInfo) freeSpinInfo.textContent = `Бонус: ${step.freeSpinsTotal} FS`;
                await sleep(phasePauseMs);
                continue;
            }

            if (step.type === 'freespinstart') {
                await padBonusSpinIfNeeded();
                bonusSpinStartedAt = Date.now();
                if (freeSpinInfo) freeSpinInfo.textContent = `FS ${step.freeSpin} / ${step.freeSpinsTotal}`;
                setGlobalMult(step.globalMultiplier ?? 0);
                continue;
            }

            if (step.type === 'grid' || step.type === 'cascade') {
                renderGrid(step.grid);
                setGlobalMult(step.globalMultiplier ?? 0);
                await sleep(step.type === 'cascade' ? cascadeDelayMs : stepDelayMs * 4);
                continue;
            }

            if (step.type === 'highlight') {
                const win = step.win || {};
                const winCells = win.cells ?? win.Cells ?? [];
                highlightCells(winCells);
                revealLightningMultipliers(winCells);
                setGlobalMult(step.globalMultiplier ?? 0);
                await sleep(highlightDelayMs);
                continue;
            }

            if (step.type === 'win') {
                const win = step.win || {};
                const payout = Number(win.payout ?? win.Payout ?? 0);
                const lm = Number(win.lightningMultiplier ?? win.LightningMultiplier ?? 1);
                const gm = Number(win.globalMultiplierApplied ?? win.GlobalMultiplierApplied ?? 1);
                let msg = `+${payout.toLocaleString('ru-RU', { minimumFractionDigits: 2 })} ₽`;
                if (lm > 1) msg += ` (⚡×${lm})`;
                if (gm > 1) msg += ` [общий ×${gm}]`;
                showResult(msg, 'win');
                await sleep(winDelayMs);
                continue;
            }

            if (step.type === 'globalmult') {
                setGlobalMult(step.globalMultiplier ?? 0);
                await sleep(stepDelayMs * 3);
                continue;
            }

            if (step.type === 'retrigger') {
                const added = Number(step.freeSpinsAdded ?? 0);
                const total = Number(step.freeSpinsTotal ?? 0);
                const sc = Number(step.scatterCount ?? 0);
                if (freeSpinInfo) freeSpinInfo.textContent = `Ретригер! +${added} FS (всего ${total})`;
                showResult(`💎 ${sc} скаттеров — ретригер +${added} FS!`, 'win');
                setGlobalMult(step.globalMultiplier ?? 0);
                await sleep(phasePauseMs);
                continue;
            }

            if (step.type === 'clearall') {
                if (phaseKey === 'main' && triggersBonus) {
                    if (phaseWinnings > 0) {
                        balanceRef.value += phaseWinnings;
                        updateBalance(balanceRef.value);
                        showResult(`Выигрыш ${phaseWinnings.toLocaleString('ru-RU', { minimumFractionDigits: 2 })} ₽`, 'win');
                    }
                    await sleep(phasePauseMs);
                }
                cells.forEach((c) => c.classList.add('fruitland-cell-fadeout'));
                await sleep(280);
                clearGrid();
                await sleep(phasePauseMs);
                continue;
            }

            if (step.type === 'bonusend') {
                await padBonusSpinIfNeeded();
                setGlobalMult(step.globalMultiplier ?? 0);
                if (freeSpinInfo) freeSpinInfo.textContent = '';
                await sleep(phasePauseMs);
                continue;
            }
        }

        await padBonusSpinIfNeeded();
        inBonusPhase = false;
    }

    async function playPhases(phases, startBalance) {
        clearGrid();
        setGlobalMult(0);
        if (bonusBanner) bonusBanner.hidden = true;
        if (freeSpinInfo) freeSpinInfo.textContent = '';

        const balanceRef = { value: startBalance };

        for (let i = 0; i < phases.length; i++) {
            const phase = phases[i];
            const phaseKey = String(phase.phase ?? phase.Phase ?? '').toLowerCase();
            const triggersBonus = !!(phase.triggersBonus ?? phase.TriggersBonus);
            const winnings = Number(phase.winnings ?? phase.Winnings ?? 0);

            await playPhase(phase, balanceRef);

            if (winnings > 0 && !(phaseKey === 'main' && triggersBonus)) {
                balanceRef.value += winnings;
                updateBalance(balanceRef.value);
                if (phaseKey === 'main')
                    showResult(`Выигрыш ${winnings.toLocaleString('ru-RU', { minimumFractionDigits: 2 })} ₽`, 'win');
            }

            const nextPhase = phases[i + 1];
            const nextKey = nextPhase ? String(nextPhase.phase ?? nextPhase.Phase ?? '').toLowerCase() : '';
            if (triggersBonus && phaseKey === 'main' && nextKey === 'bonus' && bonusBanner)
                bonusBanner.hidden = false;
        }
    }

    async function notifyComplete() {
        if (!completeUrl || !token) return;
        try {
            const body = new URLSearchParams();
            body.append('__RequestVerificationToken', token);
            await fetch(completeUrl, { method: 'POST', body });
        } catch { /* ignore */ }
    }

    async function doSpin(mode) {
        busy = true;
        updateStakeAvailability();
        showResult(mode === 'spin' ? 'Крутим...' : 'Запускаем бонус...', 'info');

        const body = new URLSearchParams();
        body.append('amount', String(getSelectedStake()));
        body.append('__RequestVerificationToken', token);

        if (mode === 'buy10') body.append('spins', '10');
        else if (mode === 'buy15') body.append('spins', '15');
        else if (mode === 'buy20') body.append('spins', '20');

        const url = mode === 'spin' ? spinUrl : buyUrl;

        try {
            const res = await fetch(url, { method: 'POST', body });
            const data = await res.json();
            if (!data.success) {
                showResult(data.message || 'Ошибка', 'error');
                return;
            }

            const finalBalance = Number(data.balance ?? data.Balance);
            const totalWinnings = Number(data.totalWinnings ?? data.TotalWinnings ?? 0);
            const startBalance = finalBalance - totalWinnings;

            updateBalance(startBalance);
            await playPhases(data.phases ?? data.Phases ?? [], startBalance);
            updateBalance(finalBalance);
            showResult(data.message || 'Готово', totalWinnings > 0 ? 'win' : 'info');
        } catch {
            showResult('Ошибка сети', 'error');
        } finally {
            await notifyComplete();
            busy = false;
            updateStakeAvailability();
        }
    }
});
