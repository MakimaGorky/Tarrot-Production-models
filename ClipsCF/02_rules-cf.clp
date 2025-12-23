;;; 02_rules.clp
;;; Логика и Машина состояний

;;; FUNCTION: Расчет коэффициента уверенности
(deffunction calc-cf (?cf1 ?cf2 ?weight)
   (* (min ?cf1 ?cf2) ?weight)
)

;; ==============================================
;; УТИЛИТЫ И ВВОД/ВЫВОД
;; ==============================================

(defrule init-ioproxy
   (declare (salience 100))
   (not (ioproxy))
   =>
   (set-strategy random)
   (assert (ioproxy (messages) (answers)))
   (printout t "Strategy set to RANDOM" crlf)
)

(defrule clear-messages-cmd
   (declare (salience 200))
   ?f <- (clearmessage)
   ?io <- (ioproxy)
   =>
   (retract ?f)
   (modify ?io (messages))
)

(defrule reject-duplicate-input
   (declare (salience 501))
   ?raw <- (raw-input-card (user-text ?u-txt))
   (card (id ?id) (aliases $? ?u-txt $?))
   (spread-slot (card-id ?id))
   ?io <- (ioproxy (messages $?msgs))
   =>
   (retract ?raw)
   (modify ?io (messages (create$ $?msgs (str-cat "Карта " ?id " уже есть в раскладе!"))))
)

(defrule resolve-raw-input
   (declare (salience 500))
   ?raw <- (raw-input-card (user-text ?u-txt) (time-modifier ?time) (cf ?input-cf))
   (card (id ?real-id) (aliases $? ?u-txt $?))
   ?io <- (ioproxy (messages $?msgs))
   =>
   (retract ?raw)
   (assert (spread-slot (card-id ?real-id) (time-modifier ?time) (cf ?input-cf)))
   (modify ?io (messages (create$ $?msgs (str-cat "Принято: " ?real-id " (" ?time ") CF=" ?input-cf))))
)

;; ==============================================
;; ЭТАП 1: START (Приветствие)
;; ==============================================

(defrule welcome
   ?st <- (stage (value start))
   ?io <- (ioproxy)
   =>
   (retract ?st)
   (assert (stage (value input-wait)))
   (modify ?io 
      (messages (str-cat "Оракул готов. Введите карты и, если нужно, уверенность (0.0-1.0)."))
      (answers "Начать" "Выход")
   )
)

(defrule user-says-start
   ?st <- (stage (value input-wait))
   ?a <- (answer (text ?t))
   (test (or (eq ?t "Начать") (eq ?t "начат")))
   =>
   (retract ?a ?st)
   (assert (stage (value generate-spread)))
   (assert (clearmessage))
)

;; ==============================================
;; ЭТАП 2: ГЕНЕРАЦИЯ РАСКЛАДА
;; ==============================================

(defrule manual-input-starts
   ?st <- (stage (value input-wait))
   (spread-slot)
   ?io <- (ioproxy)
   =>
   (retract ?st)
   (assert (stage (value collecting-manual)))
   (modify ?io (answers "Готово" "Сброс")) 
)

(defrule user-finish-manual
   ?st <- (stage (value collecting-manual))
   ?a <- (answer (text ?t))
   (test (or (eq ?t "Готово") (eq ?t "готово") (eq ?t "Анализ") (eq ?t "анализ")))
   =>
   (retract ?st ?a)
   (assert (stage (value processing)))
   (assert (clearmessage))
)

(defrule pick-cards
   ?st <- (stage (value generate-spread))
   (not (spread-slot)) 
   (deck (card-ids $?ids))
   ?io <- (ioproxy)
   =>
   (bind ?len (length$ $?ids))
   (bind ?i1 (mod (random) ?len)) (bind ?i1 (+ ?i1 1)) (bind ?c1 (nth$ ?i1 $?ids))
   (bind ?i2 (mod (random) ?len)) (bind ?i2 (+ ?i2 1)) (bind ?c2 (nth$ ?i2 $?ids))
   (bind ?i3 (mod (random) ?len)) (bind ?i3 (+ ?i3 1)) (bind ?c3 (nth$ ?i3 $?ids))

   ;; Случайные карты имеют 100% уверенность (1.0)
   (assert (spread-slot (card-id ?c1) (time-modifier "Прошлое") (cf 1.0)))
   (assert (spread-slot (card-id ?c2) (time-modifier "Настоящее") (cf 1.0)))
   (assert (spread-slot (card-id ?c3) (time-modifier "Будущее") (cf 1.0)))
   
   (retract ?st)
   (assert (stage (value processing)))

   (bind ?full-text (str-cat "Расклад: " ?c1 ", " ?c2 ", " ?c3))
   (modify ?io (messages ?full-text))
)

;; ==============================================
;; ЭТАП 3: ЛОГИЧЕСКИЙ ВЫВОД (Уровни 1, 2, 3, 4)
;; ==============================================

;; --- Уровень 1: Активация аспектов ---

(defrule L1-activate-aspects
   (stage (value processing))
   (spread-slot (card-id ?id) (time-modifier ?time) (cf ?c))
   (card (id ?id) (aspect ?asp))
   =>
   ;; Связь Карта->Аспект имеет вес 0.95
   (assert (active-aspect (cf (* ?c 0.95)) (name ?asp) (time ?time) (card-id ?id)))
)

;; --- Уровень 2: Правила синтеза (Combinations) ---

(defrule L2-skill-chaos "Мастерство + Неизвестность"
   (active-aspect (cf ?c1) (name "Мастерство") (time ?t1))
   (active-aspect (cf ?c2) (name "Неизвестность") (time ?t2))
   (test (neq ?t1 ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Рискованный стартап") (times ?t1 ?t2) (source "Маг+Шут"))))

(defrule L2-skill-mystery "Мастерство + Тайна"
   (active-aspect (cf ?c1) (name "Мастерство") (time ?t1))
   (active-aspect (cf ?c2) (name "Тайна") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Оккультные практики") (times ?t1 ?t2) (source "Маг+Жрица"))))

(defrule L2-skill-abundance "Мастерство + Изобилие"
   (active-aspect (cf ?c1) (name "Мастерство") (time ?t1))
   (active-aspect (cf ?c2) (name "Изобилие") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Реализация ресурсов") (times ?t1 ?t2) (source "Маг+Императрица"))))

(defrule L2-skill-control "Мастерство + Контроль"
   (active-aspect (cf ?c1) (name "Мастерство") (time ?t1))
   (active-aspect (cf ?c2) (name "Контроль") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Профессиональное управление") (times ?t1 ?t2) (source "Маг+Император"))))

(defrule L2-skill-change "Мастерство + Перемены"
   (active-aspect (cf ?c1) (name "Мастерство") (time ?t1))
   (active-aspect (cf ?c2) (name "Перемены") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Адаптация") (times ?t1 ?t2) (source "Маг+Колесо"))))

(defrule L2-chaos-control "Неизвестность + Контроль"
   (active-aspect (cf ?c1) (name "Неизвестность") (time ?t1))
   (active-aspect (cf ?c2) (name "Контроль") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Нарушение правил") (times ?t1 ?t2) (source "Шут+Император"))))

(defrule L2-chaos-tradition "Неизвестность + Традиция"
   (active-aspect (cf ?c1) (name "Неизвестность") (time ?t1))
   (active-aspect (cf ?c2) (name "Традиция") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Ересь или новаторство") (times ?t1 ?t2) (source "Шут+Иерофант"))))

(defrule L2-chaos-drive "Неизвестность + Драйв"
   (active-aspect (cf ?c1) (name "Неизвестность") (time ?t1))
   (active-aspect (cf ?c2) (name "Драйв") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Безумная гонка") (times ?t1 ?t2) (source "Шут+Колесница"))))

(defrule L2-mystery-search "Тайна + Поиск"
   (active-aspect (cf ?c1) (name "Тайна") (time ?t1))
   (active-aspect (cf ?c2) (name "Поиск") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Глубинный самоанализ") (times ?t1 ?t2) (source "Жрица+Отшельник"))))

(defrule L2-mystery-choice "Тайна + Выбор"
   (active-aspect (cf ?c1) (name "Тайна") (time ?t1))
   (active-aspect (cf ?c2) (name "Выбор") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Интуитивное решение") (times ?t1 ?t2) (source "Жрица+Влюбленные"))))

(defrule L2-mystery-abundance "Тайна + Изобилие"
   (active-aspect (cf ?c1) (name "Тайна") (time ?t1))
   (active-aspect (cf ?c2) (name "Изобилие") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Скрытая беременность (идеей)") (times ?t1 ?t2) (source "Жрица+Императрица"))))

(defrule L2-control-drive "Контроль + Драйв"
   (active-aspect (cf ?c1) (name "Контроль") (time ?t1))
   (active-aspect (cf ?c2) (name "Драйв") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Военная стратегия") (times ?t1 ?t2) (source "Император+Колесница"))))

(defrule L2-control-strength "Контроль + Стойкость"
   (active-aspect (cf ?c1) (name "Контроль") (time ?t1))
   (active-aspect (cf ?c2) (name "Стойкость") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Непоколебимая власть") (times ?t1 ?t2) (source "Император+Сила"))))

(defrule L2-choice-tradition "Выбор + Традиция"
   (active-aspect (cf ?c1) (name "Выбор") (time ?t1))
   (active-aspect (cf ?c2) (name "Традиция") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Брак по расчету") (times ?t1 ?t2) (source "Влюбленные+Иерофант"))))

(defrule L2-choice-change "Выбор + Перемены"
   (active-aspect (cf ?c1) (name "Выбор") (time ?t1))
   (active-aspect (cf ?c2) (name "Перемены") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Судьбоносная развилка") (times ?t1 ?t2) (source "Влюбленные+Колесо"))))

(defrule L2-drive-search "Драйв + Поиск"
   (active-aspect (cf ?c1) (name "Драйв") (time ?t1))
   (active-aspect (cf ?c2) (name "Поиск") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Паломничество") (times ?t1 ?t2) (source "Колесница+Отшельник"))))

(defrule L2-drive-strength "Драйв + Стойкость"
   (active-aspect (cf ?c1) (name "Драйв") (time ?t1))
   (active-aspect (cf ?c2) (name "Стойкость") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Прорыв через боль") (times ?t1 ?t2) (source "Колесница+Сила"))))

(defrule L2-abundance-change "Изобилие + Перемены"
   (active-aspect (cf ?c1) (name "Изобилие") (time ?t1))
   (active-aspect (cf ?c2) (name "Перемены") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Сезон урожая") (times ?t1 ?t2) (source "Императрица+Колесо"))))

(defrule L2-abundance-tradition "Изобилие + Традиция"
   (active-aspect (cf ?c1) (name "Изобилие") (time ?t1))
   (active-aspect (cf ?c2) (name "Традиция") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Семейные ценности") (times ?t1 ?t2) (source "Императрица+Иерофант"))))

(defrule L2-search-change "Поиск + Перемены"
   (active-aspect (cf ?c1) (name "Поиск") (time ?t1))
   (active-aspect (cf ?c2) (name "Перемены") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Поиск выхода в хаосе") (times ?t1 ?t2) (source "Отшельник+Колесо"))))

(defrule L2-law-chaos "Закон + Неизвестность"
   (active-aspect (cf ?c1) (name "Закон") (time ?t1))
   (active-aspect (cf ?c2) (name "Неизвестность") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Проблемы с законом по глупости") (times ?t1 ?t2) (source "Справедливость+Шут"))))

(defrule L2-law-collapse "Закон + Крах"
   (active-aspect (cf ?c1) (name "Закон") (time ?t1))
   (active-aspect (cf ?c2) (name "Крах") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Суровое наказание") (times ?t1 ?t2) (source "Справедливость+Башня"))))

(defrule L2-rebirth-completion "Возрождение + Завершение"
   (active-aspect (cf ?c1) (name "Возрождение") (time ?t1))
   (active-aspect (cf ?c2) (name "Завершение") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Переход на новый уровень бытия") (times ?t1 ?t2) (source "Суд+Мир"))))

(defrule L2-sacrifice-hope "Жертва + Надежда"
   (active-aspect (cf ?c1) (name "Жертва") (time ?t1))
   (active-aspect (cf ?c2) (name "Надежда") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Мученичество ради идеала") (times ?t1 ?t2) (source "Повешенный+Звезда"))))

(defrule L2-transform-rebirth "Трансформация + Возрождение"
   (active-aspect (cf ?c1) (name "Трансформация") (time ?t1))
   (active-aspect (cf ?c2) (name "Возрождение") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Кардинальная смена личности") (times ?t1 ?t2) (source "Смерть+Суд"))))

(defrule L2-collapse-clarity "Крах + Ясность"
   (active-aspect (cf ?c1) (name "Крах") (time ?t1))
   (active-aspect (cf ?c2) (name "Ясность") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Озарение через боль") (times ?t1 ?t2) (source "Башня+Солнце"))))

(defrule L2-temptation-choice "Искушение + Выбор"
   (active-aspect (cf ?c1) (name "Искушение") (time ?t1))
   (active-aspect (cf ?c2) (name "Выбор") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Роковая ошибка") (times ?t1 ?t2) (source "Дьявол+Влюбленные"))))

(defrule L2-temptation-illusion "Искушение + Иллюзия"
   (active-aspect (cf ?c1) (name "Искушение") (time ?t1))
   (active-aspect (cf ?c2) (name "Иллюзия") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Самообман и зависимости") (times ?t1 ?t2) (source "Дьявол+Луна"))))

(defrule L2-illusion-mystery "Иллюзия + Тайна"
   (active-aspect (cf ?c1) (name "Иллюзия") (time ?t1))
   (active-aspect (cf ?c2) (name "Тайна") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Потеря в подсознании") (times ?t1 ?t2) (source "Луна+Жрица"))))

(defrule L2-balance-strength "Баланс + Стойкость"
   (active-aspect (cf ?c1) (name "Баланс") (time ?t1))
   (active-aspect (cf ?c2) (name "Стойкость") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Исцеление") (times ?t1 ?t2) (source "Умеренность+Сила"))))

(defrule L2-hope-clarity "Надежда + Ясность"
   (active-aspect (cf ?c1) (name "Надежда") (time ?t1))
   (active-aspect (cf ?c2) (name "Ясность") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Исполнение мечты") (times ?t1 ?t2) (source "Звезда+Солнце"))))

(defrule L2-completion-drive "Завершение + Драйв"
   (active-aspect (cf ?c1) (name "Завершение") (time ?t1))
   (active-aspect (cf ?c2) (name "Драйв") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Международная экспансия") (times ?t1 ?t2) (source "Мир+Колесница"))))

(defrule L2-transform-control "Трансформация + Контроль"
   (active-aspect (cf ?c1) (name "Трансформация") (time ?t1))
   (active-aspect (cf ?c2) (name "Контроль") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Смена власти") (times ?t1 ?t2) (source "Смерть+Император"))))

(defrule L2-sacrifice-skill "Жертва + Мастерство"
   (active-aspect (cf ?c1) (name "Жертва") (time ?t1))
   (active-aspect (cf ?c2) (name "Мастерство") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Нестандартный подход") (times ?t1 ?t2) (source "Повешенный+Маг"))))

(defrule L2-collapse-abundance "Крах + Изобилие"
   (active-aspect (cf ?c1) (name "Крах") (time ?t1))
   (active-aspect (cf ?c2) (name "Изобилие") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Банкротство") (times ?t1 ?t2) (source "Башня+Императрица"))))

(defrule L2-clarity-search "Ясность + Поиск"
   (active-aspect (cf ?c1) (name "Ясность") (time ?t1))
   (active-aspect (cf ?c2) (name "Поиск") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Нахождение истины") (times ?t1 ?t2) (source "Солнце+Отшельник"))))

(defrule L2-temptation-tradition "Искушение + Традиция"
   (active-aspect (cf ?c1) (name "Искушение") (time ?t1))
   (active-aspect (cf ?c2) (name "Традиция") (time ?t2))
   =>
   (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning "Лжепророк или секта") (times ?t1 ?t2) (source "Дьявол+Иерофант"))))

;;; --- LEVEL 3: Интерпретация во времени (вес 0.9) ---

(defrule L3-skill-past
   (active-aspect (cf ?c) (name "Мастерство") (time "Прошлое"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "В прошлом вы уже подобрали ключ к этой ситуации.") (time "Прошлое") (topic "Skill"))))

(defrule L3-skill-future
   (active-aspect (cf ?c) (name "Мастерство") (time "Будущее"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Вам придется проявить ловкость рук, чтобы открыть дверь.") (time "Будущее") (topic "Action"))))

(defrule L3-chaos-present
   (active-aspect (cf ?c) (name "Неизвестность") (time "Настоящее"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Сейчас вы стоите на краю, старые карты не работают.") (time "Настоящее") (topic "Risk"))))

(defrule L3-chaos-past
   (active-aspect (cf ?c) (name "Неизвестность") (time "Прошлое"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Все началось со спонтанного, глупого решения.") (time "Прошлое") (topic "Origin"))))

(defrule L3-syn-startup-past
   (synthesis (cf ?c) (meaning "Рискованный стартап") (times $? "Прошлое" $?))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Ваше начинание базировалось на чистом энтузиазме.") (time "Прошлое") (topic "Startup"))))

(defrule L3-syn-startup-future
   (synthesis (cf ?c) (meaning "Рискованный стартап") (times $? "Будущее" $?))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Впереди новый проект, где нет гарантий, но есть свобода.") (time "Будущее") (topic "NewBiz"))))

(defrule L3-syn-mgmt-present
   (synthesis (cf ?c) (meaning "Профессиональное управление") (times $? "Настоящее" $?))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Ситуация полностью под контролем, структура работает.") (time "Настоящее") (topic "Control"))))

(defrule L3-change-present
   (active-aspect (cf ?c) (name "Перемены") (time "Настоящее"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Дверь вращается прямо сейчас. Кто не успел, тот опоздал.") (time "Настоящее") (topic "Urgency"))))

(defrule L3-change-future
   (active-aspect (cf ?c) (name "Перемены") (time "Будущее"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Стабильности не ждите, готовьтесь к качке.") (time "Будущее") (topic "Warning"))))

(defrule L3-strength-past
   (active-aspect (cf ?c) (name "Стойкость") (time "Прошлое"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Вы выдержали давление, и это ваш фундамент.") (time "Прошлое") (topic "Base"))))

(defrule L3-choice-future
   (active-aspect (cf ?c) (name "Выбор") (time "Будущее"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Скоро придется выбирать одну дверь из двух.") (time "Будущее") (topic "Decision"))))

(defrule L3-syn-intuition-choice
   (synthesis (cf ?c) (meaning "Интуитивное решение"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Логика здесь бессильна, сердце знает верный вход.") (time "Общее") (topic "Heart"))))

(defrule L3-syn-war-future
   (synthesis (cf ?c) (meaning "Военная стратегия") (times $? "Будущее" $?))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Вам предстоит захват новых территорий жесткими методами.") (time "Будущее") (topic "War"))))

(defrule L3-search-present
   (active-aspect (cf ?c) (name "Поиск") (time "Настоящее"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Вы ищете дверь на ощупь в темноте.") (time "Настоящее") (topic "Lost"))))

(defrule L3-syn-adapt-past
   (synthesis (cf ?c) (meaning "Адаптация") (times $? "Прошлое" $?))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Вы удачно подстроились под прошлые изменения.") (time "Прошлое") (topic "Flexibility"))))

(defrule L3-syn-family
   (synthesis (cf ?c) (meaning "Семейные ценности"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Традиции и семья — ваша главная опора сейчас.") (time "Общее") (topic "Family"))))

(defrule L3-abundance-future
   (active-aspect (cf ?c) (name "Изобилие") (time "Будущее"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Двери вашего дома будут открыты для гостей и плодов труда.") (time "Будущее") (topic "Harvest"))))

(defrule L3-syn-race-present
   (synthesis (cf ?c) (meaning "Безумная гонка") (times $? "Настоящее" $?))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "События несутся с огромной скоростью без тормозов.") (time "Настоящее") (topic "Speed"))))

(defrule L3-law-past
   (active-aspect (cf ?c) (name "Закон") (time "Прошлое"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Вы пожинаете плоды прошлых юридических решений.") (time "Прошлое") (topic "Legal"))))

(defrule L3-law-future
   (active-aspect (cf ?c) (name "Закон") (time "Будущее"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Вас ждет проверка или экзамен. Все тайное станет явным.") (time "Будущее") (topic "Exam"))))

(defrule L3-sacrifice-present
   (active-aspect (cf ?c) (name "Жертва") (time "Настоящее"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Сейчас дверь закрыта. Любое движение только затянет узлы.") (time "Настоящее") (topic "Stagnation"))))

(defrule L3-transform-future
   (active-aspect (cf ?c) (name "Трансформация") (time "Будущее"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Грядет полная зачистка. То, что отжило, уйдет навсегда.") (time "Будущее") (topic "EndGame"))))

(defrule L3-transform-past
   (active-aspect (cf ?c) (name "Трансформация") (time "Прошлое"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Вы пережили тяжелую утрату или резкую перемену.") (time "Прошлое") (topic "Trauma"))))

(defrule L3-balance-present
   (active-aspect (cf ?c) (name "Баланс") (time "Настоящее"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Ситуация требует ювелирной точности и терпения.") (time "Настоящее") (topic "Patience"))))

(defrule L3-temptation-present
   (active-aspect (cf ?c) (name "Искушение") (time "Настоящее"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Вы находитесь в плену своих желаний или манипулятора.") (time "Настоящее") (topic "Trap"))))

(defrule L3-collapse-future
   (active-aspect (cf ?c) (name "Крах") (time "Будущее"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Ваши планы построены на песке и скоро рухнут.") (time "Будущее") (topic "Danger"))))

(defrule L3-hope-future
   (active-aspect (cf ?c) (name "Надежда") (time "Будущее"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Впереди свет. Ваши мечты наконец обретут форму.") (time "Будущее") (topic "Inspiration"))))

(defrule L3-illusion-present
   (active-aspect (cf ?c) (name "Иллюзия") (time "Настоящее"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Вы блуждаете в тумане. Не верьте своим глазам.") (time "Настоящее") (topic "Deception"))))

(defrule L3-clarity-present
   (active-aspect (cf ?c) (name "Ясность") (time "Настоящее"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Все предельно ясно. У вас карт-бланш на счастье.") (time "Настоящее") (topic "Success"))))

(defrule L3-rebirth-past
   (active-aspect (cf ?c) (name "Возрождение") (time "Прошлое"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Вы услышали зов и проснулись от долгого сна.") (time "Прошлое") (topic "Awakening"))))

(defrule L3-completion-future
   (active-aspect (cf ?c) (name "Завершение") (time "Будущее"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Гештальт будет закрыт. Вы получите всё, к чему шли.") (time "Будущее") (topic "Achievement"))))

(defrule L3-syn-punishment
   (synthesis (cf ?c) (meaning "Суровое наказание") (times $? "Будущее" $?))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Ошибки прошлого приведут к неминуемому краху.") (time "Будущее") (topic "Karma"))))

(defrule L3-syn-healing
   (synthesis (cf ?c) (meaning "Исцеление") (times $? "Настоящее" $?))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Вы медленно, но верно восстанавливаете силы.") (time "Настоящее") (topic "Health"))))

(defrule L3-syn-obsession
   (synthesis (cf ?c) (meaning "Самообман и зависимости"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Вы добровольно вошли в клетку иллюзий.") (time "Общее") (topic "Addiction"))))

(defrule L3-syn-bankruptcy
   (synthesis (cf ?c) (meaning "Банкротство"))
   =>
   (assert (context-interpretation (cf (* ?c 0.9)) (text "Ресурсы истощены, старая структура больше не кормит.") (time "Общее") (topic "Loss"))))

;;; --- LEVEL 4: Финальный совет ---

;; Пример правила с CF
(defrule general-interpretation-to-advice
   (stage (value processing))
   (context-interpretation (text ?text) (cf ?c))
   (not (final-advice)) 
   =>
   (assert (final-advice (text ?text) (cf ?c)))
)

;; ==============================================
;; ЭТАП 4: ОТЧЕТ (Вывод пользователю)
;; ==============================================

(defrule collection-done
   (declare (salience -20))
   ?st <- (stage (value processing))
   ?io <- (ioproxy (messages $?old-msgs))
   (final-advice (text ?advice) (cf ?c))
   =>
   (bind ?percent (round (* ?c 100)))
   (bind ?msg (str-cat "Совет карт (" ?percent "%): " ?advice))
   
   (retract ?st)
   (assert (stage (value finished)))
   
   (modify ?io 
       (messages (create$ $?old-msgs ?msg))
       (answers "Спасибо" "Сброс")
   )
)

(defrule reset-cmd
   ?st <- (stage (value finished))
   ?a <- (answer (text ?t))
   (test (or (eq ?t "Сброс") (eq ?t "сброс")))
   =>
   (reset)
)