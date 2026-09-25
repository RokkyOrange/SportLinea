document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('camelotForm');
    if (!form) return;

    const spinBtn = document.getElementById('camelotSpinBtn');
    const buyBtn = document.getElementById('camelotBuyBtn');
    const featureBtn = document.getElementById('camelotFeatureBtn');
    const balanceEl = document.getElementById('camelotBalance');
    const amountInput = document.getElementById('camelotAmount');
    const stakeChips = [...document.querySelectorAll('.camelot-stake-chip')];
    const resultBox = document.getElementById('camelotResult');
    const buyHint = document.getElementById('camelotBuyPriceHint');
    const featureHint = document.getElementById('camelotFeaturePriceHint');
    const bonusBanner = document.getElementById('camelotBonusBanner');
    const cells = [...document.querySelectorAll('.camelot-cell')];

    const spinUrl = form.dataset.spinUrl;
    const buyUrl = form.dataset.buyUrl;
    const featureUrl = form.dataset.featureUrl;
    const completeUrl = form.dataset.completeUrl;
    const buyMultiplier = parseInt(form.dataset.buyMultiplier || '100', 10);
    const featureMultiplier = parseInt(form.dataset.featureMultiplier || '10', 10);
    const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value;

    let busy = false;
    const revealDelayMs = 85;
    const collectorWavePauseMs = 1000;
    const coinFadeOutMs = 280;

    spinBtn?.addEventListener('click', () => { if (!busy) doSpin('spin'); });
    featureBtn?.addEventListener('click', () => { if (!busy) doSpin('feature'); });
    buyBtn?.addEventListener('click', () => { if (!busy) doSpin('bonus'); });

    stakeChips.forEach((chip) => {
        chip.addEventListener('click', () => {
            if (chip.disabled || busy) return;
            selectStake(parseInt(chip.dataset.amount, 10));
        });
    });

    updateStakeAvailability();
    updateBuyHints();

    window.addEventListener('sportlinea:balance', (event) => {
        if (event.detail?.source === 'camelot') return;
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
        const stake = getSelectedStake();
        if (featureHint) {
            featureHint.textContent = stake > 0
                ? `Покупка функции: ${(stake * featureMultiplier).toLocaleString('ru-RU')} ₽ · гарантированный коллектор`
                : '';
        }
        if (buyHint) {
            buyHint.textContent = stake > 0
                ? `Покупка бонуса: ${(stake * buyMultiplier).toLocaleString('ru-RU')} ₽`
                : '';
        }
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

        const canFeature = selected > 0 && balance >= selected * featureMultiplier;
        const canBuy = selected > 0 && balance >= selected * buyMultiplier;
        if (spinBtn) spinBtn.disabled = busy || !highestAffordable;
        if (featureBtn) featureBtn.disabled = busy || !canFeature;
        if (buyBtn) buyBtn.disabled = busy || !canBuy;
    }

    function updateBalance(balance) {
        if (window.SportLineaBalance) {
            window.SportLineaBalance.sync(balance, 'camelot');
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

    function normalizeReveal(reveal) {
        if (!reveal) return null;
        const kind = pick(reveal, 'kind', 'Kind');
        return {
            row: Number(pick(reveal, 'row', 'Row')),
            col: Number(pick(reveal, 'col', 'Col')),
            kind: kind == null ? '' : String(kind).toLowerCase(),
            nominal: pick(reveal, 'nominal', 'Nominal'),
            multiplier: pick(reveal, 'multiplier', 'Multiplier')
        };
    }

    function normalizeCollector(collector) {
        if (!collector) return null;
        return {
            row: pick(collector, 'row', 'Row'),
            col: pick(collector, 'col', 'Col'),
            collectedNominal: pick(collector, 'collectedNominal', 'CollectedNominal')
        };
    }

    function normalizeMultiplier(multiplier) {
        if (!multiplier) return null;
        return {
            row: pick(multiplier, 'row', 'Row'),
            col: pick(multiplier, 'col', 'Col'),
            factor: pick(multiplier, 'factor', 'Factor')
        };
    }

    function normalizeSteps(phase) {
        const raw = phase.steps ?? phase.Steps ?? [];
        if (!raw.length) return buildLegacySteps(phase);
        return raw.map((step) => ({
            type: (pick(step, 'type', 'Type') || '').toLowerCase(),
            reveal: normalizeReveal(pick(step, 'reveal', 'Reveal')),
            collector: normalizeCollector(pick(step, 'collector', 'Collector')),
            multiplier: normalizeMultiplier(pick(step, 'multiplier', 'Multiplier')),
            keepCollectors: (pick(step, 'keepCollectors', 'KeepCollectors') || [])
                .map(normalizeCollector)
                .filter(Boolean),
            updatedNominals: (pick(step, 'updatedNominals', 'UpdatedNominals') || [])
                .map(normalizeReveal)
                .filter(Boolean)
        }));
    }

    function shouldKeepCollectorCell(cell, keepCollectors) {
        const row = parseInt(cell.dataset.row, 10);
        const col = parseInt(cell.dataset.col, 10);
        if (keepCollectors.length > 0)
            return keepCollectors.some((c) => c.row === row && c.col === col);
        return isCollectorCell(cell);
    }

    function renderCollectorCell(cell, collector, showLetter) {
        if (!cell) return;
        const inner = cell.querySelector('.camelot-cell-inner');
        cell.className = 'camelot-cell camelot-cell-revealed camelot-collector';
        if (!showLetter && collector.collectedNominal) {
            cell.classList.add('camelot-collector-active');
            if (inner) {
                inner.classList.remove('camelot-collector-letter');
                inner.textContent = formatNominal(collector.collectedNominal);
            }
        } else if (inner) {
            cell.classList.remove('camelot-collector-active');
            inner.textContent = 'К';
            inner.classList.add('camelot-collector-letter');
        }
    }

    function shouldKeepCellOnClear(cell, keepCollectors) {
        if (shouldKeepCollectorCell(cell, keepCollectors)) return true;
        return getCellKind(cell) === 'scatter';
    }

    async function clearCoinsKeepingCollectors(step) {
        const keepCollectors = step?.keepCollectors ?? [];

        cells.forEach((cell) => {
            if (shouldKeepCellOnClear(cell, keepCollectors)) return;
            if (!cell.classList.contains('camelot-cell-revealed')) return;
            cell.classList.add('camelot-cell-fadeout');
        });
        await sleep(coinFadeOutMs);

        cells.forEach((cell) => {
            if (shouldKeepCellOnClear(cell, keepCollectors)) return;
            cell.className = 'camelot-cell';
            const inner = cell.querySelector('.camelot-cell-inner');
            if (inner) {
                inner.textContent = '';
                inner.classList.remove('camelot-collector-letter');
            }
        });

        keepCollectors.forEach((collector) => {
            const cell = getCell(collector.row, collector.col);
            renderCollectorCell(cell, collector, !collector.collectedNominal);
        });
    }

    function clearAllCells() {
        cells.forEach((cell) => {
            cell.className = 'camelot-cell';
            const inner = cell.querySelector('.camelot-cell-inner');
            if (inner) inner.textContent = '';
        });
    }

    async function clearEntireField() {
        cells.forEach((cell) => {
            if (cell.classList.contains('camelot-cell-revealed'))
                cell.classList.add('camelot-cell-fadeout');
        });
        await sleep(coinFadeOutMs);
        clearAllCells();
    }

    function resetGrid() {
        clearAllCells();
        if (bonusBanner) bonusBanner.hidden = true;
    }

    function getCell(row, col) {
        const r = Number(row);
        const c = Number(col);
        if (!Number.isFinite(r) || !Number.isFinite(c)) return null;
        return cells.find((el) =>
            parseInt(el.dataset.row, 10) === r &&
            parseInt(el.dataset.col, 10) === c);
    }

    function isCollectorCell(cell) {
        return cell.classList.contains('camelot-collector') ||
            cell.classList.contains('camelot-collector-active');
    }

    function getCellKind(cell) {
        if (!cell) return null;
        if (cell.classList.contains('camelot-scatter')) return 'scatter';
        if (cell.classList.contains('camelot-mult')) return 'multiplier';
        if (cell.classList.contains('camelot-collector')) return 'collector';
        if (cell.classList.contains('camelot-gold')) return 'gold';
        if (cell.classList.contains('camelot-silver')) return 'silver';
        if (cell.classList.contains('camelot-bronze')) return 'bronze';
        return null;
    }

    function applyCellSnapshot(reveal) {
        if (!reveal || reveal.row == null || reveal.col == null) return;
        const cell = getCell(reveal.row, reveal.col);
        if (!cell || !cell.classList.contains('camelot-cell-revealed')) return;

        const kind = reveal.kind || '';
        const currentKind = getCellKind(cell);
        if (currentKind === 'scatter' || currentKind === 'multiplier') return;

        if (kind === 'collector') {
            if (currentKind !== 'collector') return;
            renderCollectorCell(cell, { collectedNominal: reveal.nominal }, !reveal.nominal);
            return;
        }

        if (kind === 'bronze' || kind === 'silver' || kind === 'gold') {
            if (currentKind !== kind) return;
            const inner = cell.querySelector('.camelot-cell-inner');
            if (inner && reveal.nominal != null)
                inner.textContent = formatNominal(reveal.nominal);
        }
    }

    function renderCell(reveal) {
        if (!reveal) return;
        const cell = getCell(reveal.row, reveal.col);
        if (!cell) return;
        const inner = cell.querySelector('.camelot-cell-inner');
        cell.className = 'camelot-cell';
        cell.classList.add('camelot-cell-revealed', `camelot-${reveal.kind}`, 'camelot-cell-pop');

        if (reveal.kind === 'scatter') {
            if (inner) inner.textContent = '🏰';
        } else if (reveal.kind === 'collector') {
            renderCollectorCell(cell, { collectedNominal: 0 }, true);
        } else if (reveal.kind === 'multiplier') {
            if (inner) inner.textContent = `×${reveal.multiplier}`;
            cell.classList.add('camelot-mult');
        } else if (reveal.nominal != null) {
            if (inner) inner.textContent = formatNominal(reveal.nominal);
        }
    }

    function formatNominal(n) {
        const num = Number(n);
        if (Number.isInteger(num)) return String(num);
        return num.toLocaleString('ru-RU', { maximumFractionDigits: 2 });
    }

    function sleep(ms) {
        return new Promise((resolve) => setTimeout(resolve, ms));
    }

    async function playPhase(phase, balanceRef) {
        const phaseKey = String(phase.phase ?? phase.Phase ?? '').toLowerCase();
        const triggersBonus = !!(phase.triggersBonus ?? phase.TriggersBonus);
        const phaseWinnings = Number(phase.winnings ?? phase.Winnings ?? 0);

        if (phaseKey === 'bonus' && bonusBanner)
            bonusBanner.hidden = false;

        const steps = normalizeSteps(phase);

        for (const step of steps) {
            if (step.type === 'reveal' && step.reveal) {
                renderCell(step.reveal);
                await sleep(revealDelayMs);
                continue;
            }

            if (step.type === 'collect' && step.collector) {
                const cell = getCell(step.collector.row, step.collector.col);
                renderCollectorCell(cell, step.collector, false);
                await sleep(revealDelayMs * 3);
                continue;
            }

            if (step.type === 'clear') {
                await clearCoinsKeepingCollectors(step);
                await sleep(collectorWavePauseMs);
                continue;
            }

            if (step.type === 'clearall') {
                if (phaseKey === 'main' && triggersBonus) {
                    if (phaseWinnings > 0) {
                        balanceRef.value += phaseWinnings;
                        updateBalance(balanceRef.value);
                        showResult(`Выигрыш ${phaseWinnings.toLocaleString('ru-RU', { minimumFractionDigits: 2 })} ₽`, 'win');
                    }
                    await sleep(collectorWavePauseMs);
                }
                await clearEntireField();
                await sleep(collectorWavePauseMs);
                continue;
            }

            if (step.type === 'multiply' && step.multiplier) {
                cells.forEach((cell) => {
                    const kind = getCellKind(cell);
                    if (!cell.classList.contains('camelot-cell-revealed')) return;
                    if (kind === 'bronze' || kind === 'silver' || kind === 'gold' || kind === 'collector')
                        cell.classList.add('camelot-multiplied');
                });
                const mCell = getCell(step.multiplier.row, step.multiplier.col);
                if (mCell) {
                    mCell.className = 'camelot-cell camelot-cell-revealed camelot-mult';
                    const inner = mCell.querySelector('.camelot-cell-inner');
                    if (inner) inner.textContent = `×${step.multiplier.factor}`;
                }
                for (const updated of step.updatedNominals || [])
                    applyCellSnapshot(updated);
                await sleep(revealDelayMs * 3);
                continue;
            }
        }
    }

    function buildLegacySteps(phase) {
        const steps = [];
        for (const reveal of phase.reveals ?? phase.Reveals ?? [])
            steps.push({ type: 'reveal', reveal: normalizeReveal(reveal) });
        for (const collector of phase.collectors ?? phase.Collectors ?? [])
            steps.push({ type: 'collect', collector: normalizeCollector(collector) });
        for (const multiplier of phase.multipliers ?? phase.Multipliers ?? [])
            steps.push({ type: 'multiply', multiplier: normalizeMultiplier(multiplier) });
        return steps;
    }

    async function playPhases(phases, startBalance) {
        resetGrid();
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
            } else if (triggersBonus && phaseKey === 'main' && winnings <= 0) {
                await sleep(collectorWavePauseMs);
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
        showResult(mode === 'feature' ? 'Запускаем функцию...' : 'Крутим...', 'info');

        const url = mode === 'bonus' ? buyUrl : mode === 'feature' ? featureUrl : spinUrl;
        const body = new URLSearchParams();
        body.append('amount', String(getSelectedStake()));
        body.append('__RequestVerificationToken', token);

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
