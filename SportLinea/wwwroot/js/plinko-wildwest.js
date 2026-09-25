document.addEventListener('DOMContentLoaded', () => {
    const form = document.getElementById('plinkoForm');
    if (!form) return;

    const spinBtn = document.getElementById('plinkoSpinBtn');
    const buyBountyBtn = document.getElementById('plinkoBuyBountyBtn');
    const buyBountyPriceEl = document.getElementById('plinkoBuyBountyPrice');
    const plinkoBuyBonusGroup = document.getElementById('plinkoBuyBonusGroup');
    const balanceEl = document.getElementById('plinkoBalance');
    const amountInput = document.getElementById('plinkoAmount');
    const stakeOptions = document.getElementById('plinkoStakeOptions');
    const stakeChips = stakeOptions ? [...stakeOptions.querySelectorAll('.plinko-stake-chip')] : [];
    const tiles = [...document.querySelectorAll('.plinko-tile')];
    const bountyPlates = [...document.querySelectorAll('.plinko-bounty-plate')];
    const resultBox = document.getElementById('plinkoResult');
    const mainPanel = document.getElementById('plinkoMainPanel');
    const bountyPanel = document.getElementById('plinkoBountyPanel');
    const bountyPrompt = document.getElementById('plinkoBountyPrompt');
    const bountyPromptText = document.getElementById('plinkoBountyPromptText');
    const bountyPromptBtn = document.getElementById('plinkoBountyPromptBtn');
    const stakeGroup = document.getElementById('plinkoStakeGroup');
    const panelTitle = document.getElementById('plinkoPanelTitle');
    const panelDesc = document.getElementById('plinkoPanelDesc');

    const spinUrl = form.dataset.spinUrl;
    const pickUrl = form.dataset.pickUrl;
    const bountyUrl = form.dataset.bountyUrl;
    const buyBonusUrl = form.dataset.buyBountyUrl;
    const bonusBuyMultiplier = parseInt(form.dataset.bountyBuyMultiplier || '5', 10);
    const freeBountyStake = parseInt(form.dataset.freeBountyStake || '200', 10);
    const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const useFreeBountyCheck = document.getElementById('useFreePlinkoBounty');

    let roundActive = false;
    let bountyPending = false;
    let busy = false;
    let bountyPromptMode = null;
    let pendingBountyCompleteData = null;

    spinBtn?.addEventListener('click', () => {
        if (busy || roundActive || bountyPending) return;
        doSpin();
    });

    buyBountyBtn?.addEventListener('click', () => {
        if (busy || roundActive || bountyPending) return;
        doBuyBounty();
    });

    useFreeBountyCheck?.addEventListener('change', () => {
        updateStakeControlsForFreeBounty();
        updateBuyPrices();
    });

    stakeChips.forEach((chip) => {
        chip.addEventListener('click', () => {
            if (chip.disabled || roundActive || bountyPending || isFreeBountySelected()) return;
            selectStake(parseInt(chip.dataset.amount, 10));
            updateBuyPrices();
        });
    });

    tiles.forEach((tile) => {
        tile.addEventListener('click', () => {
            if (!roundActive || busy) return;
            pickTile(parseInt(tile.dataset.index, 10));
        });
    });

    bountyPlates.forEach((plate) => {
        plate.addEventListener('click', () => {
            if (!bountyPending || busy || bountyPromptMode) return;
            bountyShoot(parseInt(plate.dataset.target, 10));
        });
    });

    bountyPromptBtn?.addEventListener('click', () => handleBountyPromptContinue());

    updateStakeAvailability();
    updateBuyPrices();

    window.addEventListener('sportlinea:balance', (event) => {
        if (event.detail?.source === 'plinko') return;
        updateStakeAvailability();
        updateBuyPrices();
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
        updateBuyPrices();
    }

    function updateStakeAvailability() {
        const balance = getBalance();
        let selected = getSelectedStake();
        let highestAffordable = null;

        stakeChips.forEach((chip) => {
            const amount = parseInt(chip.dataset.amount, 10);
            const affordable = amount <= balance;
            chip.disabled = !affordable;
            chip.classList.toggle('disabled', !affordable);
            if (affordable) highestAffordable = amount;
        });

        if (!selected || selected > balance) {
            selected = highestAffordable || parseInt(stakeChips[0]?.dataset.amount || '10', 10);
            selectStake(selected);
        } else {
            selectStake(selected);
        }

        if (spinBtn) spinBtn.disabled = roundActive || bountyPending || (!highestAffordable && !isFreeBountySelected());
        if (isFreeBountySelected()) updateStakeControlsForFreeBounty();
    }

    function setStakeControlsEnabled(enabled) {
        stakeChips.forEach((chip) => {
            if (!chip.classList.contains('disabled')) {
                chip.disabled = !enabled;
            }
        });
    }

    function isFreeBountySelected() {
        return useFreeBountyCheck?.checked === true;
    }

    function updateStakeControlsForFreeBounty() {
        const useFree = isFreeBountySelected();
        if (useFree) {
            selectStake(freeBountyStake);
        }
        stakeChips.forEach((chip) => {
            chip.disabled = useFree || chip.classList.contains('disabled');
        });
    }

    function canAffordBuyBonus() {
        if (isFreeBountySelected()) return true;
        const amount = getSelectedStake();
        if (!amount) return false;
        return amount * bonusBuyMultiplier <= getBalance();
    }

    function setBuyControlsEnabled(enabled) {
        if (!plinkoBuyBonusGroup || bountyPending || roundActive) return;
        const canBuy = enabled && canAffordBuyBonus();
        if (buyBountyBtn) buyBountyBtn.disabled = !canBuy;
    }

    function updatePlinkoBuySectionVisibility() {
        if (!plinkoBuyBonusGroup) return;
        plinkoBuyBonusGroup.style.display = (roundActive || bountyPending) ? 'none' : 'block';
    }

    function setMainControlsEnabled(enabled) {
        if (spinBtn) spinBtn.disabled = !enabled || roundActive || bountyPending;
        setBuyControlsEnabled(enabled);
        if (!roundActive && !bountyPending) {
            setStakeControlsEnabled(enabled);
        }
    }

    function resetMainControls() {
        busy = false;
        if (spinBtn) {
            spinBtn.disabled = false;
            spinBtn.textContent = '🎲 Новый раунд';
        }
        updateStakeAvailability();
        updateBuyPrices();
    }

    function updateBuyPrices() {
        const amount = getSelectedStake();
        const useFree = isFreeBountySelected();
        const priceText = useFree
            ? 'Бесплатно'
            : amount > 0
                ? `${(amount * bonusBuyMultiplier).toLocaleString('ru-RU')} ₽`
                : '—';

        if (buyBountyPriceEl) buyBountyPriceEl.textContent = priceText;
        updatePlinkoBuySectionVisibility();
        setBuyControlsEnabled(!busy && !roundActive && !bountyPending);
    }

    function updateBalance(balance) {
        if (window.SportLineaBalance) {
            window.SportLineaBalance.sync(balance, 'plinko');
        } else if (balanceEl) {
            balanceEl.textContent = balance.toLocaleString('ru-RU', { minimumFractionDigits: 2 }) + ' ₽';
        }
        updateStakeAvailability();
        updateBuyPrices();
    }

    function showResult(text, type) {
        resultBox.style.display = 'block';
        resultBox.className = 'roulette-result mt-3 result-' + type;
        resultBox.textContent = text;
    }

    function renderTileSymbol(frontEl, emoji) {
        if (!frontEl) return;
        frontEl.className = 'plinko-tile-front';
        frontEl.textContent = emoji;
    }

    function resetTiles() {
        tiles.forEach((tile) => {
            tile.disabled = true;
            tile.classList.remove('flipped', 'plinko-tile-hit');
            const front = tile.querySelector('.plinko-tile-front');
            if (front) {
                front.textContent = '';
                front.className = 'plinko-tile-front';
            }
        });
    }

    function resetBountyPlates() {
        bountyPlates.forEach((plate) => {
            plate.disabled = true;
            plate.classList.remove('shot');
            const outcome = plate.querySelector('.plinko-bounty-outcome');
            if (outcome) {
                outcome.hidden = true;
                outcome.textContent = '';
                outcome.className = 'plinko-bounty-outcome';
            }
        });
    }

    async function doSpin() {
        const amount = getSelectedStake();
        if (!amount || amount > getBalance()) {
            showResult('Недостаточно средств для выбранной ставки', 'danger');
            return;
        }

        busy = true;
        setMainControlsEnabled(false);
        spinBtn.textContent = 'Раздача...';

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
            resetTiles();
            tiles.forEach((t) => { t.disabled = false; });

            mainPanel.style.display = 'block';
            bountyPanel.style.display = 'none';
            stakeGroup.style.display = 'none';
            updatePlinkoBuySectionVisibility();
            spinBtn.style.display = 'none';

            panelTitle.textContent = data.bonusHunt ? '⭐ Bonus Hunt!' : 'Выберите 3 фишки';
            panelDesc.textContent = data.message;
            showResult(data.message, data.bonusHunt ? 'jackpot' : 'success');
        } catch {
            showResult('Ошибка соединения с сервером', 'danger');
            resetMainControls();
        }

        busy = false;
        spinBtn.textContent = '🎲 Новый раунд';
    }

    function showBonusToast(message) {
        if (!message) return;

        document.getElementById('plinkoBonusToast')?.remove();

        const container = document.querySelector('.site-main .container');
        if (!container) return;

        const alert = document.createElement('div');
        alert.id = 'plinkoBonusToast';
        alert.className = 'alert alert-bonus alert-dismissible fade show';
        alert.setAttribute('role', 'alert');
        alert.innerHTML = `${message}<button type="button" class="btn-close" data-bs-dismiss="alert" aria-label="Закрыть"></button>`;

        const flashAlerts = container.querySelector('.alert');
        if (flashAlerts) {
            flashAlerts.insertAdjacentElement('beforebegin', alert);
        } else {
            container.insertBefore(alert, container.firstChild);
        }

        alert.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    }

    function hideFreeBountyOption() {
        if (!useFreeBountyCheck) return;
        useFreeBountyCheck.checked = false;
        useFreeBountyCheck.closest('.form-check')?.remove();
        updateStakeAvailability();
        updateBuyPrices();
    }

    async function doBuyBounty() {
        if (!buyBonusUrl) return;

        const useFree = isFreeBountySelected();
        const amount = useFree ? freeBountyStake : getSelectedStake();
        if (!amount) {
            showResult('Выберите ставку', 'danger');
            return;
        }

        const price = amount * bonusBuyMultiplier;
        if (!useFree && price > getBalance()) {
            showResult(`Недостаточно средств. Нужно ${price.toLocaleString('ru-RU')} ₽`, 'danger');
            return;
        }

        const confirmText = useFree
            ? `Запустить бесплатную Bounty Hunter?\nНоминал бонуса: ${amount.toLocaleString('ru-RU')} ₽`
            : `Купить Bounty Hunter за ${price.toLocaleString('ru-RU')} ₽?\nНоминал бонуса: ${amount.toLocaleString('ru-RU')} ₽`;

        if (!confirm(confirmText)) {
            return;
        }

        setBuyControlsEnabled(false);
        spinBtn.disabled = true;

        const body = new FormData();
        body.append('__RequestVerificationToken', token);
        body.append('amount', amount);
        if (useFree) body.append('useFreeBounty', 'true');

        try {
            const response = await fetch(buyBonusUrl, { method: 'POST', body });
            const data = await response.json();

            if (!data.success) {
                showResult(data.message, 'danger');
                resetMainControls();
                return;
            }

            updateBalance(data.balance);
            if (data.freePlinkoBountyUsed) hideFreeBountyOption();
            showBonusToast(data.bonusMessage);
            resultBox.style.display = 'none';
            startBountyMode(data.message, data.bountySpinsRemaining, data.bountyLevel);
        } catch {
            showResult('Ошибка соединения с сервером', 'danger');
            resetMainControls();
        }
    }

    async function pickTile(index) {
        busy = true;
        tiles.forEach((t) => { t.disabled = true; });

        const body = new FormData();
        body.append('__RequestVerificationToken', token);
        body.append('tileIndex', index);

        try {
            const response = await fetch(pickUrl, { method: 'POST', body });
            const data = await response.json();
            if (!data.success) {
                showResult(data.message, 'danger');
                tiles.forEach((t) => { if (!t.classList.contains('flipped')) t.disabled = false; });
                busy = false;
                return;
            }

            const tile = tiles.find((t) => parseInt(t.dataset.index, 10) === index);
            if (tile) {
                tile.classList.add('flipped', 'plinko-tile-hit');
                renderTileSymbol(tile.querySelector('.plinko-tile-front'), data.emoji);
            }

            updateBalance(data.balance);

            if (data.bountyTriggered) {
                roundActive = false;
                resultBox.style.display = 'none';
                showBonusToast(data.bonusMessage);
                startBountyMode(data.message, data.bountySpinsRemaining, data.bountyLevel);
            } else if (data.roundComplete) {
                roundActive = false;
                endMainRound(data);
            } else {
                showResult(`Открыто: ${data.emoji} ${data.symbol}. Осталось: ${data.picksRemaining}`, 'success');
                tiles.forEach((t) => { if (!t.classList.contains('flipped')) t.disabled = false; });
            }
        } catch {
            showResult('Ошибка соединения', 'danger');
            tiles.forEach((t) => { if (!t.classList.contains('flipped')) t.disabled = false; });
        }

        busy = false;
    }

    function endMainRound(data) {
        const type = data.winnings > 0 ? 'jackpot' : 'danger';
        showResult(data.message, type);
        stakeGroup.style.display = 'block';
        updatePlinkoBuySectionVisibility();
        spinBtn.style.display = 'block';
        panelTitle.textContent = 'Дикий Запад';
        panelDesc.textContent = 'Выберите ставку и начните новый раунд.';
        resetMainControls();
    }

    function hideBountyPrompt() {
        bountyPromptMode = null;
        if (bountyPrompt) bountyPrompt.hidden = true;
    }

    function showBountyPrompt(mode, data) {
        bountyPromptMode = mode;
        if (!bountyPrompt || !bountyPromptText || !bountyPromptBtn) return;

        if (mode === 'levelUp') {
            bountyPromptText.textContent = data.message;
            bountyPromptBtn.textContent = 'Продолжить';
        } else {
            pendingBountyCompleteData = data;
            bountyPromptText.textContent = data.message;
            bountyPromptBtn.textContent = 'Далее';
        }

        bountyPrompt.hidden = false;
    }

    function handleBountyPromptContinue() {
        const mode = bountyPromptMode;
        hideBountyPrompt();

        if (mode === 'levelUp') {
            resetBountyPlates();
            bountyPlates.forEach((p) => { p.disabled = false; });
            resultBox.style.display = 'none';
            return;
        }

        if (mode === 'complete' && pendingBountyCompleteData) {
            finishBountyMode(pendingBountyCompleteData);
            pendingBountyCompleteData = null;
        }
    }

    function startBountyMode(message, spinsRemaining, level) {
        bountyPending = true;
        roundActive = false;
        busy = false;
        hideBountyPrompt();
        pendingBountyCompleteData = null;

        mainPanel.style.display = 'none';
        bountyPanel.style.display = 'block';
        stakeGroup.style.display = 'none';
        updatePlinkoBuySectionVisibility();
        spinBtn.style.display = 'none';

        panelTitle.textContent = 'Bounty Hunter';
        panelDesc.textContent = 'Стреляйте по тарелкам. Соберите 6 пуль для перехода на следующий уровень.';
        updateBountyHud(level || 1, 0, spinsRemaining, 0);
        resetBountyPlates();
        bountyPlates.forEach((p) => { p.disabled = false; });
        showResult(message, 'jackpot');
    }

    function updateBountyHud(level, bullets, spins, multSum) {
        document.getElementById('bountyLevel').textContent = level;
        document.getElementById('bountyBullets').textContent = bullets;
        document.getElementById('bountySpins').textContent = spins;
        document.getElementById('bountyMultSum').textContent = multSum;
    }

    function formatBulletOutcome(bulletsGained) {
        return `🔫 +${bulletsGained}`;
    }

    async function bountyShoot(targetIndex) {
        busy = true;
        bountyPlates.forEach((p) => { p.disabled = true; });

        const body = new FormData();
        body.append('__RequestVerificationToken', token);
        body.append('targetIndex', targetIndex);

        try {
            const response = await fetch(bountyUrl, { method: 'POST', body });
            const data = await response.json();
            if (!data.success) {
                showResult(data.message, 'danger');
                bountyPlates.forEach((p) => { if (!p.classList.contains('shot')) p.disabled = false; });
                busy = false;
                return;
            }

            const plate = bountyPlates.find((p) => parseInt(p.dataset.target, 10) === targetIndex);
            if (plate) {
                plate.classList.add('shot');
                const outcome = plate.querySelector('.plinko-bounty-outcome');
                if (outcome) {
                    outcome.hidden = false;
                    outcome.textContent = data.isBullet
                        ? formatBulletOutcome(data.bulletsGained)
                        : data.outcomeLabel;
                }
            }

            updateBalance(data.balance);
            updateBountyHud(data.level, data.bulletsOnLevel, data.spinsRemaining, data.totalMultiplierSum);

            if (data.bonusComplete) {
                showResult(data.message, data.winnings > 0 ? 'jackpot' : 'danger');
                showBountyPrompt('complete', data);
            } else if (data.levelAdvanced) {
                showResult(
                    data.isBullet ? formatBulletOutcome(data.bulletsGained) : data.outcomeLabel,
                    data.isBullet ? 'success' : data.outcomeLabel === '×0' ? 'danger' : 'jackpot'
                );
                showBountyPrompt('levelUp', data);
            } else {
                showResult(data.message, data.isBullet ? 'success' : data.outcomeLabel === '×0' ? 'danger' : 'jackpot');
                bountyPlates.forEach((p) => { if (!p.classList.contains('shot')) p.disabled = false; });
            }
        } catch {
            showResult('Ошибка соединения', 'danger');
            bountyPlates.forEach((p) => { if (!p.classList.contains('shot')) p.disabled = false; });
        }

        busy = false;
    }

    function finishBountyMode(data) {
        bountyPending = false;
        hideBountyPrompt();
        pendingBountyCompleteData = null;
        bountyPanel.style.display = 'none';
        mainPanel.style.display = 'block';
        resetTiles();
        stakeGroup.style.display = 'block';
        updatePlinkoBuySectionVisibility();
        spinBtn.style.display = 'block';
        panelTitle.textContent = 'Дикий Запад';
        panelDesc.textContent = 'Бонус завершён. Начните новый раунд.';
        showResult(data.message, data.winnings > 0 ? 'jackpot' : 'danger');
        resetMainControls();
    }
});
