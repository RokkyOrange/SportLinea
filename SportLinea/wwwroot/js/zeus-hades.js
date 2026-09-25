document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('zeusForm');
    if (!form) return;

    const spinBtn = document.getElementById('zeusSpinBtn');
    const collectBtn = document.getElementById('zeusCollectBtn');
    const balanceEl = document.getElementById('zeusBalance');
    const amountInput = document.getElementById('zeusAmount');
    const stakeOptions = document.getElementById('zeusStakeOptions');
    const stakeChips = stakeOptions ? [...stakeOptions.querySelectorAll('.zeus-stake-chip')] : [];
    const resultBox = document.getElementById('zeusResult');
    const stakeGroup = document.getElementById('zeusStakeGroup');
    const panelTitle = document.getElementById('zeusPanelTitle');
    const panelDesc = document.getElementById('zeusPanelDesc');
    const confrontationBanner = document.getElementById('zeusConfrontationBanner');
    const confrontationProgress = document.getElementById('zeusConfrontationProgress');
    const turboToggle = document.getElementById('zeusTurboToggle');
    const turboGroup = document.getElementById('zeusTurboGroup');
    const rowBtns = [...document.querySelectorAll('.zeus-row-btn')];
    const colBtns = [...document.querySelectorAll('.zeus-col-btn')];
    const cells = [...document.querySelectorAll('.zeus-cell')];

    const spinUrl = form.dataset.spinUrl;
    const revealUrl = form.dataset.revealUrl;
    const completeUrl = form.dataset.completeUrl;
    const confrontationUrl = form.dataset.confrontationUrl;
    const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value;

    let roundActive = false;
    let confrontationActive = false;
    let busy = false;
    let turboRunning = false;
    let turboConfrontationPending = false;

    const TURBO_STORAGE_KEY = 'zeus-turbo-enabled';
    const TURBO_REVEAL_DELAY_MS = 70;

    if (turboToggle) {
        turboToggle.checked = localStorage.getItem(TURBO_STORAGE_KEY) === '1';
        turboToggle.addEventListener('change', () => {
            localStorage.setItem(TURBO_STORAGE_KEY, turboToggle.checked ? '1' : '0');
        });
    }

    function isTurboEnabled() {
        return !!turboToggle?.checked;
    }

    function setTurboControlsEnabled(enabled) {
        if (turboToggle) turboToggle.disabled = !enabled;
        if (turboGroup) turboGroup.classList.toggle('disabled', !enabled);
    }

    spinBtn?.addEventListener('click', () => {
        if (busy || roundActive || confrontationActive) return;
        doSpin();
    });

    collectBtn?.addEventListener('click', () => {
        if (busy || !roundActive) return;
        doComplete();
    });

    [...rowBtns, ...colBtns].forEach((btn) => {
        btn.addEventListener('click', () => {
            if (busy || turboRunning || !roundActive || confrontationActive) return;
            const isRow = btn.dataset.isRow === 'true';
            const index = parseInt(btn.dataset.index, 10);
            revealLine(isRow, index, btn);
        });
    });

    stakeChips.forEach((chip) => {
        chip.addEventListener('click', () => {
            if (chip.disabled || roundActive || confrontationActive) return;
            selectStake(parseInt(chip.dataset.amount, 10));
        });
    });

    updateStakeAvailability();

    window.addEventListener('sportlinea:balance', (event) => {
        if (event.detail?.source === 'zeus') return;
        updateStakeAvailability();
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
    }

    function updateStakeAvailability() {
        const balance = getBalance();
        let selected = getSelectedStake();
        let highestAffordable = null;

        stakeChips.forEach((chip) => {
            const amount = parseInt(chip.dataset.amount, 10);
            const affordable = amount <= balance;
            chip.disabled = !affordable || roundActive || confrontationActive;
            chip.classList.toggle('disabled', !affordable);
            if (affordable) highestAffordable = amount;
        });

        if (!selected || selected > balance) {
            selected = highestAffordable || parseInt(stakeChips[0]?.dataset.amount || '10', 10);
            selectStake(selected);
        }

        if (spinBtn) spinBtn.disabled = busy || roundActive || confrontationActive || !highestAffordable;
        setTurboControlsEnabled(!busy && !roundActive && !confrontationActive);
    }

    function setRoundControls(enabled) {
        stakeChips.forEach((chip) => {
            if (!chip.classList.contains('disabled')) chip.disabled = !enabled;
        });
        if (spinBtn) spinBtn.disabled = !enabled || roundActive || confrontationActive;
    }

    function updateBalance(balance) {
        if (window.SportLineaBalance) {
            window.SportLineaBalance.sync(balance, 'zeus');
        } else if (balanceEl) {
            balanceEl.textContent = balance.toLocaleString('ru-RU', { minimumFractionDigits: 2 }) + ' ₽';
        }
        updateStakeAvailability();
    }

    function showResult(text, type) {
        resultBox.style.display = 'block';
        resultBox.className = 'roulette-result mt-3 result-' + type;
        resultBox.textContent = text;
    }

    function resetLineButtons() {
        [...rowBtns, ...colBtns].forEach((btn) => {
            btn.disabled = true;
            btn.classList.remove('revealed');
            const valueEl = btn.querySelector('.zeus-line-value');
            const iconEl = btn.querySelector('.zeus-line-icon');
            if (valueEl) {
                valueEl.hidden = true;
                valueEl.textContent = '';
            }
            if (iconEl) iconEl.hidden = false;
        });
    }

    function resetGrid() {
        cells.forEach((cell) => {
            cell.classList.remove('zeus-cell-win');
            const emoji = cell.querySelector('.zeus-cell-emoji');
            const hit = cell.querySelector('.zeus-cell-hit');
            if (emoji) emoji.textContent = '?';
            if (hit) {
                hit.hidden = true;
                hit.textContent = '';
            }
        });
    }

    function renderGrid(emojis) {
        cells.forEach((cell) => {
            const r = parseInt(cell.dataset.row, 10);
            const c = parseInt(cell.dataset.col, 10);
            const emoji = emojis?.[r]?.[c] || '?';
            const emojiEl = cell.querySelector('.zeus-cell-emoji');
            if (emojiEl) emojiEl.textContent = emoji;
        });
    }

    function highlightHits(hits) {
        if (!hits?.length) return;
        hits.forEach((hit) => {
            const cell = cells.find((el) =>
                parseInt(el.dataset.row, 10) === hit.row &&
                parseInt(el.dataset.col, 10) === hit.col);
            if (!cell) return;
            cell.classList.add('zeus-cell-win');
            const hitEl = cell.querySelector('.zeus-cell-hit');
            if (hitEl) {
                hitEl.hidden = false;
                hitEl.textContent = `×${hit.pickedMultiplier}`;
            }
        });
    }

    function revealLineButton(btn, multiplier) {
        btn.classList.add('revealed');
        btn.disabled = true;
        const iconEl = btn.querySelector('.zeus-line-icon');
        const valueEl = btn.querySelector('.zeus-line-value');
        if (iconEl) iconEl.hidden = true;
        if (valueEl) {
            valueEl.hidden = false;
            valueEl.textContent = `×${multiplier}`;
        }
    }

    function enableLineButtons() {
        rowBtns.forEach((btn) => {
            if (!btn.classList.contains('revealed')) btn.disabled = false;
        });
        colBtns.forEach((btn) => {
            if (!btn.classList.contains('revealed')) btn.disabled = false;
        });
    }

    function showAllLineMultipliers(rowMults, colMults) {
        rowBtns.forEach((btn, i) => revealLineButton(btn, rowMults[i] ?? 0));
        colBtns.forEach((btn, i) => revealLineButton(btn, colMults[i] ?? 0));
    }

    async function doSpin() {
        const amount = getSelectedStake();
        if (!amount || amount > getBalance()) {
            showResult('Недостаточно средств для выбранной ставки', 'danger');
            return;
        }

        busy = true;
        setRoundControls(false);
        setTurboControlsEnabled(false);
        spinBtn.textContent = 'Крутим...';
        collectBtn.style.display = 'none';
        confrontationBanner.hidden = true;

        const body = new FormData();
        body.append('__RequestVerificationToken', token);
        body.append('amount', amount);

        try {
            const response = await fetch(spinUrl, { method: 'POST', body });
            const data = await response.json();
            if (!data.success) {
                showResult(data.message, 'danger');
                resetMainControls();
                return;
            }

            updateBalance(data.balance);
            roundActive = true;
            resetLineButtons();
            resetGrid();
            renderGrid(data.gridEmojis);
            if (!isTurboEnabled()) enableLineButtons();

            stakeGroup.style.display = 'none';
            if (turboGroup) turboGroup.style.display = 'none';
            spinBtn.style.display = 'none';

            panelTitle.textContent = data.confrontationQueued ? '⚔ Confrontation ждёт!' : 'Откройте линии';
            panelDesc.textContent = data.message;
            showResult(data.message, data.confrontationQueued ? 'jackpot' : 'success');

            turboConfrontationPending = !!data.confrontationQueued;
            if (isTurboEnabled()) {
                busy = false;
                await runTurboRound();
                return;
            }
        } catch {
            showResult('Ошибка соединения с сервером', 'danger');
            resetMainControls();
        }

        busy = false;
        spinBtn.textContent = '⚡ Крутить!';
    }

    async function runTurboRound() {
        turboRunning = true;
        setTurboControlsEnabled(false);

        try {
            for (let i = 0; i < rowBtns.length; i++) {
                if (!roundActive) return;
                const ok = await revealLine(true, i, rowBtns[i]);
                if (!ok) {
                    if (roundActive) enableLineButtons();
                    return;
                }
                await delay(TURBO_REVEAL_DELAY_MS);
            }

            for (let i = 0; i < colBtns.length; i++) {
                if (!roundActive) return;
                const ok = await revealLine(false, i, colBtns[i]);
                if (!ok) {
                    if (roundActive) enableLineButtons();
                    return;
                }
                await delay(TURBO_REVEAL_DELAY_MS);
            }

            if (turboConfrontationPending) {
                collectBtn.style.display = 'block';
                panelTitle.textContent = '⚔ Confrontation ждёт!';
                panelDesc.textContent = 'Все линии открыты — соберите выигрыш!';
                showResult('Confrontation ждёт! Соберите выигрыш.', 'jackpot');
                return;
            }

            await doComplete();
        } finally {
            turboRunning = false;
            updateStakeAvailability();
        }
    }

    async function revealLine(isRow, index, btn) {
        busy = true;
        btn.disabled = true;

        const body = new FormData();
        body.append('__RequestVerificationToken', token);
        body.append('isRow', isRow ? 'true' : 'false');
        body.append('index', index);

        try {
            const response = await fetch(revealUrl, { method: 'POST', body });
            const data = await response.json();
            if (!data.success) {
                showResult(data.message, 'danger');
                if (!btn.classList.contains('revealed')) btn.disabled = false;
                busy = false;
                return false;
            }

            revealLineButton(btn, data.multiplier);
            showResult(data.message, data.multiplier > 0 ? 'success' : 'danger');

            if (data.allRevealed) {
                if (!(turboRunning && !turboConfrontationPending)) {
                    collectBtn.style.display = 'block';
                    panelDesc.textContent = turboConfrontationPending
                        ? 'Confrontation ждёт! Соберите выигрыш.'
                        : 'Все линии открыты — соберите выигрыш!';
                }
            } else if (!turboRunning) {
                enableLineButtons();
            }
        } catch {
            showResult('Ошибка соединения', 'danger');
            if (!btn.classList.contains('revealed')) btn.disabled = false;
            busy = false;
            return false;
        }

        busy = false;
        return true;
    }

    async function doComplete() {
        busy = true;
        collectBtn.disabled = true;

        const body = new FormData();
        body.append('__RequestVerificationToken', token);

        try {
            const response = await fetch(completeUrl, { method: 'POST', body });
            const data = await response.json();
            if (!data.success) {
                showResult(data.message, 'danger');
                collectBtn.disabled = false;
                busy = false;
                return;
            }

            updateBalance(data.balance);
            highlightHits(data.hits);
            showResult(data.message, data.winnings > 0 ? 'jackpot' : 'danger');

            if (data.confrontationStarted) {
                roundActive = false;
                confrontationActive = true;
                collectBtn.style.display = 'none';
                confrontationBanner.hidden = false;
                panelTitle.textContent = '⚔ Confrontation';
                panelDesc.textContent = 'Бонусная игра! Все линии открыты — смотрите пересечения.';
                await runConfrontation();
            } else {
                endRound();
            }
        } catch {
            showResult('Ошибка соединения', 'danger');
            collectBtn.disabled = false;
        }

        busy = false;
    }

    async function runConfrontation() {
        for (let i = 0; i < 3; i++) {
            if (confrontationProgress) {
                confrontationProgress.textContent = `Раунд ${i + 1}/3`;
            }

            await delay(600);

            const body = new FormData();
            body.append('__RequestVerificationToken', token);

            try {
                const response = await fetch(confrontationUrl, { method: 'POST', body });
                const data = await response.json();
                if (!data.success) {
                    showResult(data.message, 'danger');
                    break;
                }

                resetGrid();
                renderGrid(data.gridEmojis);
                showAllLineMultipliers(data.rowMultipliers, data.colMultipliers);
                await delay(400);
                highlightHits(data.hits);
                updateBalance(data.balance);
                showResult(data.message, data.roundWinnings > 0 ? 'jackpot' : 'danger');

                if (data.bonusComplete) {
                    confrontationActive = false;
                    confrontationBanner.hidden = true;
                    panelTitle.textContent = 'Олимп vs Подземелье';
                    panelDesc.textContent = `Бонус завершён. Итого: ${data.totalBonusWinnings.toLocaleString('ru-RU')} ₽`;
                    endRound();
                    break;
                }

                await delay(1200);
                resetLineButtons();
            } catch {
                showResult('Ошибка соединения в Confrontation', 'danger');
                break;
            }
        }
    }

    function endRound() {
        roundActive = false;
        confrontationActive = false;
        stakeGroup.style.display = 'block';
        spinBtn.style.display = 'block';
        collectBtn.style.display = 'none';
        collectBtn.disabled = false;
        if (turboGroup) turboGroup.style.display = 'block';
        resetMainControls();
    }

    function resetMainControls() {
        busy = false;
        roundActive = false;
        confrontationActive = false;
        if (spinBtn) {
            spinBtn.disabled = false;
            spinBtn.textContent = '⚡ Крутить!';
            spinBtn.style.display = 'block';
        }
        collectBtn.style.display = 'none';
        stakeGroup.style.display = 'block';
        if (turboGroup) turboGroup.style.display = 'block';
        confrontationBanner.hidden = true;
        panelTitle.textContent = 'Олимп vs Подземелье';
        panelDesc.textContent = 'Выберите ставку и крутите. Откройте все символы линий, чтобы собрать выигрыш.';
        updateStakeAvailability();
    }

    function delay(ms) {
        return new Promise((resolve) => setTimeout(resolve, ms));
    }
});
