;;; 02_rules.clp
;;; Логика и Машина состояний

;; ==============================================
;; УТИЛИТЫ И ВВОД/ВЫВОД
;; ==============================================

(defrule init-ioproxy
   (declare (salience 100))
   (not (ioproxy))
   =>
   ;; Сейчас рандомно разрешаем конфликт, если он возникает.
   ;; Убрать, чтобы выбирался наиболее глубокий вариант
   ;;(стандартное разрешение конфликтов)
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
   (declare (salience 501)) ;; Самый высокий приоритет при вводе
   ?raw <- (raw-input-card (user-text ?u-txt))
   ;; Проверяем: есть ли уже карта с таким алиасом в раскладе?
   (card (id ?id) (aliases $? ?u-txt $?))
   (spread-slot (card-id ?id))
   
   ?io <- (ioproxy (messages $?msgs))
   =>
   (retract ?raw)
   (modify ?io (messages (create$ $?msgs (str-cat "Карта " ?id " уже есть в раскладе!"))))
)

(defrule resolve-raw-input
   (declare (salience 500))
   ?raw <- (raw-input-card (user-text ?u-txt) (time-modifier ?time))
   ;; Ищем карту
   (card (id ?real-id) (aliases $? ?u-txt $?))
   ;; Подключаемся к интерфейсу вывода
   ?io <- (ioproxy (messages $?msgs))
   =>
   (retract ?raw)
   (assert (spread-slot (card-id ?real-id) (time-modifier ?time)))
   
   ;; ВАЖНО: Добавляем сообщение пользователю
   (modify ?io (messages (create$ $?msgs (str-cat "Принято: " ?real-id " на позицию " ?time))))
)

(defrule resolve-raw-input
   (declare (salience 500))
   ?raw <- (raw-input-card (user-text ?u-txt) (time-modifier ?time))
   (card (id ?real-id) (aliases $? ?u-txt $?))
   ?io <- (ioproxy (messages $?msgs))
   =>
   (retract ?raw)
   (assert (spread-slot (card-id ?real-id) (time-modifier ?time)))
   (modify ?io (messages (create$ $?msgs (str-cat "Принято: " ?real-id " (" ?time ")"))))
)

;; ==============================================
;; ЭТАП 0: TEST (Ультрабессилие)
;; ==============================================

;; === БЕЗУМНЫЙ ТЕСТ ===
(defrule crazy-cyberpunk-test
   ;; ПРИОРИТЕТ 1000! Это правило сработает раньше всех остальных.
   ;;(declare (salience 1000)) 
   (stage (value processing))
   
   ;; Ищем Жрицу и Колесницу
   (spread-slot (card-id "II Жрица"))
   (spread-slot (card-id "VII Колесница"))
   =>
   ;; Сразу создаем финальный совет, игнорируя промежуточные этапы
   (assert (final-advice (text "ТЕСТ УСПЕШЕН: Жрица угнала Колесницу и уехала в киберпанк!")))
   
   ;; Для отладки в консоль (если подключена)
   (printout t "!!! CRAZY RULE FIRED !!!" crlf)
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
   
   ;; Склеиваем все фразы в одну
   (modify ?io 
      (messages (str-cat "Оракул готов. Скажите 'Начать' для случайного расклада. " 
                         "Или вводите карты, например: 'Шут прошлое'. "
                         "Когда закончите ввод, скажите 'Готово'."))
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
;; ЭТАП 2: ГЕНЕРАЦИЯ РАСКЛАДА (Случайный выбор)
;; ==============================================

;; === БЛОК РУЧНОГО ВВОДА ===

;; 1. Переход в режим сбора при первой карте
(defrule manual-input-starts
   ?st <- (stage (value input-wait))
   (spread-slot) ;; Появилась первая карта
   ?io <- (ioproxy)
   =>
   (retract ?st)
   (assert (stage (value collecting-manual)))
   ;; Обновляем кнопки: теперь доступно "Готово"
   (modify ?io (answers "Готово" "Сброс")) 
)

;; 2. Команда "Готово" - переход к анализу
(defrule user-finish-manual
   ?st <- (stage (value collecting-manual))
   ?a <- (answer (text ?t))
   (test (or (eq ?t "Готово") (eq ?t "готово") (eq ?t "Анализ") (eq ?t "анализ")))
   =>
   (retract ?st ?a)
   ;; Если карт вообще нет (пользователь сразу нажал Готово), можно добавить проверку,
   ;; но пока пустим в процессинг, там выдаст дефолтный совет.
   (assert (stage (value processing)))
   (assert (clearmessage))
   (printout t "User finished input. Starting processing." crlf)
)

(defrule pick-cards
   ?st <- (stage (value generate-spread))
   (not (spread-slot)) 
   (deck (card-ids $?ids))
   ?io <- (ioproxy)
   =>
   ;; Выбор случайных карт (логика простая)
   (bind ?len (length$ $?ids))
   (bind ?i1 (mod (random) ?len)) (bind ?i1 (+ ?i1 1)) (bind ?c1 (nth$ ?i1 $?ids))
   (bind ?i2 (mod (random) ?len)) (bind ?i2 (+ ?i2 1)) (bind ?c2 (nth$ ?i2 $?ids))
   (bind ?i3 (mod (random) ?len)) (bind ?i3 (+ ?i3 1)) (bind ?c3 (nth$ ?i3 $?ids))

   (assert (spread-slot (card-id ?c1) (time-modifier "Прошлое")))
   (assert (spread-slot (card-id ?c2) (time-modifier "Настоящее")))
   (assert (spread-slot (card-id ?c3) (time-modifier "Будущее")))
   
   (retract ?st)
   (assert (stage (value processing)))

   ;; Формируем одну строку с описанием всего расклада
   (bind ?full-text (str-cat "В прошлом выпало: " ?c1 ". "
                             "Сейчас влияет: " ?c2 ". "
                             "В будущем ждет: " ?c3 ". "
                             "Анализирую связи..."))
                             
   (modify ?io (messages ?full-text))
)

;; ==============================================
;; ЭТАП 3: ЛОГИЧЕСКИЙ ВЫВОД (Уровни 1, 2, 3, 4)
;; ==============================================

;; --- Уровень 1: Активация аспектов ---

(defrule L1-activate-aspects
   (stage (value processing))
   (spread-slot (card-id ?id) (time-modifier ?time))
   (card (id ?id) (aspect ?asp))
   =>
   (assert (active-aspect (name ?asp) (time ?time) (card-id ?id)))
)

;; ---     Уровень 2: Правила синтеза (Combinations 0-10)       ---
;; --- (Пример Правила 2 из ТЗ: Маг + Шут = Творческая энергия) ---

;; ГРУППА 2.1: МАСТЕРСТВО (МАГ) + ...
(defrule L2-skill-chaos "Мастерство + Неизвестность"
   (active-aspect (name "Мастерство") (time ?t1))
   (active-aspect (name "Неизвестность") (time ?t2))
   (test (neq ?t1 ?t2)) ; Не та же самая карта
   =>
   (assert (synthesis (meaning "Рискованный стартап") (times ?t1 ?t2) (source "Маг+Шут"))))

(defrule L2-skill-mystery "Мастерство + Тайна"
   (active-aspect (name "Мастерство") (time ?t1))
   (active-aspect (name "Тайна") (time ?t2))
   =>
   (assert (synthesis (meaning "Оккультные практики") (times ?t1 ?t2) (source "Маг+Жрица"))))

(defrule L2-skill-abundance "Мастерство + Изобилие"
   (active-aspect (name "Мастерство") (time ?t1))
   (active-aspect (name "Изобилие") (time ?t2))
   =>
   (assert (synthesis (meaning "Реализация ресурсов") (times ?t1 ?t2) (source "Маг+Императрица"))))

(defrule L2-skill-control "Мастерство + Контроль"
   (active-aspect (name "Мастерство") (time ?t1))
   (active-aspect (name "Контроль") (time ?t2))
   =>
   (assert (synthesis (meaning "Профессиональное управление") (times ?t1 ?t2) (source "Маг+Император"))))

(defrule L2-skill-change "Мастерство + Перемены"
   (active-aspect (name "Мастерство") (time ?t1))
   (active-aspect (name "Перемены") (time ?t2))
   =>
   (assert (synthesis (meaning "Адаптация") (times ?t1 ?t2) (source "Маг+Колесо"))))

;; ГРУППА 2.2: НЕИЗВЕСТНОСТЬ (ШУТ) + ...
(defrule L2-chaos-control "Неизвестность + Контроль"
   (active-aspect (name "Неизвестность") (time ?t1))
   (active-aspect (name "Контроль") (time ?t2))
   =>
   (assert (synthesis (meaning "Нарушение правил") (times ?t1 ?t2) (source "Шут+Император"))))

(defrule L2-chaos-tradition "Неизвестность + Традиция"
   (active-aspect (name "Неизвестность") (time ?t1))
   (active-aspect (name "Традиция") (time ?t2))
   =>
   (assert (synthesis (meaning "Ересь или новаторство") (times ?t1 ?t2) (source "Шут+Иерофант"))))

(defrule L2-chaos-drive "Неизвестность + Драйв"
   (active-aspect (name "Неизвестность") (time ?t1))
   (active-aspect (name "Драйв") (time ?t2))
   =>
   (assert (synthesis (meaning "Безумная гонка") (times ?t1 ?t2) (source "Шут+Колесница"))))

;; ГРУППА 2.3: ТАЙНА (ЖРИЦА) + ...
(defrule L2-mystery-search "Тайна + Поиск"
   (active-aspect (name "Тайна") (time ?t1))
   (active-aspect (name "Поиск") (time ?t2))
   =>
   (assert (synthesis (meaning "Глубинный самоанализ") (times ?t1 ?t2) (source "Жрица+Отшельник"))))

(defrule L2-mystery-choice "Тайна + Выбор"
   (active-aspect (name "Тайна") (time ?t1))
   (active-aspect (name "Выбор") (time ?t2))
   =>
   (assert (synthesis (meaning "Интуитивное решение") (times ?t1 ?t2) (source "Жрица+Влюбленные"))))

(defrule L2-mystery-abundance "Тайна + Изобилие"
   (active-aspect (name "Тайна") (time ?t1))
   (active-aspect (name "Изобилие") (time ?t2))
   =>
   (assert (synthesis (meaning "Скрытая беременность (идеей)") (times ?t1 ?t2) (source "Жрица+Императрица"))))

;; ГРУППА 2.4: КОНТРОЛЬ (ИМПЕРАТОР) + ...
(defrule L2-control-drive "Контроль + Драйв"
   (active-aspect (name "Контроль") (time ?t1))
   (active-aspect (name "Драйв") (time ?t2))
   =>
   (assert (synthesis (meaning "Военная стратегия") (times ?t1 ?t2) (source "Император+Колесница"))))

(defrule L2-control-strength "Контроль + Стойкость"
   (active-aspect (name "Контроль") (time ?t1))
   (active-aspect (name "Стойкость") (time ?t2))
   =>
   (assert (synthesis (meaning "Непоколебимая власть") (times ?t1 ?t2) (source "Император+Сила"))))

;; ГРУППА 2.5: ВЫБОР (ВЛЮБЛЕННЫЕ) + ...
(defrule L2-choice-tradition "Выбор + Традиция"
   (active-aspect (name "Выбор") (time ?t1))
   (active-aspect (name "Традиция") (time ?t2))
   =>
   (assert (synthesis (meaning "Брак по расчету") (times ?t1 ?t2) (source "Влюбленные+Иерофант"))))

(defrule L2-choice-change "Выбор + Перемены"
   (active-aspect (name "Выбор") (time ?t1))
   (active-aspect (name "Перемены") (time ?t2))
   =>
   (assert (synthesis (meaning "Судьбоносная развилка") (times ?t1 ?t2) (source "Влюбленные+Колесо"))))

;; ГРУППА 2.6: ДРАЙВ (КОЛЕСНИЦА) + ...
(defrule L2-drive-search "Драйв + Поиск"
   (active-aspect (name "Драйв") (time ?t1))
   (active-aspect (name "Поиск") (time ?t2))
   =>
   (assert (synthesis (meaning "Паломничество") (times ?t1 ?t2) (source "Колесница+Отшельник"))))

(defrule L2-drive-strength "Драйв + Стойкость"
   (active-aspect (name "Драйв") (time ?t1))
   (active-aspect (name "Стойкость") (time ?t2))
   =>
   (assert (synthesis (meaning "Прорыв через боль") (times ?t1 ?t2) (source "Колесница+Сила"))))

;; ГРУППА 2.7: ИЗОБИЛИЕ (ИМПЕРАТРИЦА) + ...
(defrule L2-abundance-change "Изобилие + Перемены"
   (active-aspect (name "Изобилие") (time ?t1))
   (active-aspect (name "Перемены") (time ?t2))
   =>
   (assert (synthesis (meaning "Сезон урожая") (times ?t1 ?t2) (source "Императрица+Колесо"))))

(defrule L2-abundance-tradition "Изобилие + Традиция"
   (active-aspect (name "Изобилие") (time ?t1))
   (active-aspect (name "Традиция") (time ?t2))
   =>
   (assert (synthesis (meaning "Семейные ценности") (times ?t1 ?t2) (source "Императрица+Иерофант"))))

;; ГРУППА 2.8: ПОИСК (ОТШЕЛЬНИК) + ...
(defrule L2-search-change "Поиск + Перемены"
   (active-aspect (name "Поиск") (time ?t1))
   (active-aspect (name "Перемены") (time ?t2))
   =>
   (assert (synthesis (meaning "Поиск выхода в хаосе") (times ?t1 ?t2) (source "Отшельник+Колесо"))))

;; ГРУППА: СУДЕБНЫЕ И КАРМИЧЕСКИЕ (Справедливость, Суд)
(defrule L2-law-chaos "Закон + Неизвестность"
   (active-aspect (name "Закон") (time ?t1))
   (active-aspect (name "Неизвестность") (time ?t2))
   =>
   (assert (synthesis (meaning "Проблемы с законом по глупости") (times ?t1 ?t2) (source "Справедливость+Шут"))))

(defrule L2-law-collapse "Закон + Крах"
   (active-aspect (name "Закон") (time ?t1))
   (active-aspect (name "Крах") (time ?t2))
   =>
   (assert (synthesis (meaning "Суровое наказание") (times ?t1 ?t2) (source "Справедливость+Башня"))))

(defrule L2-rebirth-completion "Возрождение + Завершение"
   (active-aspect (name "Возрождение") (time ?t1))
   (active-aspect (name "Завершение") (time ?t2))
   =>
   (assert (synthesis (meaning "Переход на новый уровень бытия") (times ?t1 ?t2) (source "Суд+Мир"))))

;; ГРУППА: КРИЗИСНЫЕ (Повешенный, Смерть, Башня)
(defrule L2-sacrifice-hope "Жертва + Надежда"
   (active-aspect (name "Жертва") (time ?t1))
   (active-aspect (name "Надежда") (time ?t2))
   =>
   (assert (synthesis (meaning "Мученичество ради идеала") (times ?t1 ?t2) (source "Повешенный+Звезда"))))

(defrule L2-transform-rebirth "Трансформация + Возрождение"
   (active-aspect (name "Трансформация") (time ?t1))
   (active-aspect (name "Возрождение") (time ?t2))
   =>
   (assert (synthesis (meaning "Кардинальная смена личности") (times ?t1 ?t2) (source "Смерть+Суд"))))

(defrule L2-collapse-clarity "Крах + Ясность"
   (active-aspect (name "Крах") (time ?t1))
   (active-aspect (name "Ясность") (time ?t2))
   =>
   (assert (synthesis (meaning "Озарение через боль") (times ?t1 ?t2) (source "Башня+Солнце"))))

;; ГРУППА: ТЕМНЫЕ И СЛОЖНЫЕ (Дьявол, Луна)
(defrule L2-temptation-choice "Искушение + Выбор"
   (active-aspect (name "Искушение") (time ?t1))
   (active-aspect (name "Выбор") (time ?t2))
   =>
   (assert (synthesis (meaning "Роковая ошибка") (times ?t1 ?t2) (source "Дьявол+Влюбленные"))))

(defrule L2-temptation-illusion "Искушение + Иллюзия"
   (active-aspect (name "Искушение") (time ?t1))
   (active-aspect (name "Иллюзия") (time ?t2))
   =>
   (assert (synthesis (meaning "Самообман и зависимости") (times ?t1 ?t2) (source "Дьявол+Луна"))))

(defrule L2-illusion-mystery "Иллюзия + Тайна"
   (active-aspect (name "Иллюзия") (time ?t1))
   (active-aspect (name "Тайна") (time ?t2))
   =>
   (assert (synthesis (meaning "Потеря в подсознании") (times ?t1 ?t2) (source "Луна+Жрица"))))

;; ГРУППА: ПОЗИТИВНЫЕ И ГАРМОНИЧНЫЕ (Умеренность, Звезда, Солнце, Мир)
(defrule L2-balance-strength "Баланс + Стойкость"
   (active-aspect (name "Баланс") (time ?t1))
   (active-aspect (name "Стойкость") (time ?t2))
   =>
   (assert (synthesis (meaning "Исцеление") (times ?t1 ?t2) (source "Умеренность+Сила"))))

(defrule L2-hope-clarity "Надежда + Ясность"
   (active-aspect (name "Надежда") (time ?t1))
   (active-aspect (name "Ясность") (time ?t2))
   =>
   (assert (synthesis (meaning "Исполнение мечты") (times ?t1 ?t2) (source "Звезда+Солнце"))))

(defrule L2-completion-drive "Завершение + Драйв"
   (active-aspect (name "Завершение") (time ?t1))
   (active-aspect (name "Драйв") (time ?t2))
   =>
   (assert (synthesis (meaning "Международная экспансия") (times ?t1 ?t2) (source "Мир+Колесница"))))

;; ГРУППА: СМЕШАННЫЕ (Сочетание со старыми картами)
(defrule L2-transform-control "Трансформация + Контроль"
   (active-aspect (name "Трансформация") (time ?t1))
   (active-aspect (name "Контроль") (time ?t2))
   =>
   (assert (synthesis (meaning "Смена власти") (times ?t1 ?t2) (source "Смерть+Император"))))

(defrule L2-sacrifice-skill "Жертва + Мастерство"
   (active-aspect (name "Жертва") (time ?t1))
   (active-aspect (name "Мастерство") (time ?t2))
   =>
   (assert (synthesis (meaning "Нестандартный подход") (times ?t1 ?t2) (source "Повешенный+Маг"))))

(defrule L2-collapse-abundance "Крах + Изобилие"
   (active-aspect (name "Крах") (time ?t1))
   (active-aspect (name "Изобилие") (time ?t2))
   =>
   (assert (synthesis (meaning "Банкротство") (times ?t1 ?t2) (source "Башня+Императрица"))))

(defrule L2-clarity-search "Ясность + Поиск"
   (active-aspect (name "Ясность") (time ?t1))
   (active-aspect (name "Поиск") (time ?t2))
   =>
   (assert (synthesis (meaning "Нахождение истины") (times ?t1 ?t2) (source "Солнце+Отшельник"))))

(defrule L2-temptation-tradition "Искушение + Традиция"
   (active-aspect (name "Искушение") (time ?t1))
   (active-aspect (name "Традиция") (time ?t2))
   =>
   (assert (synthesis (meaning "Лжепророк или секта") (times ?t1 ?t2) (source "Дьявол+Иерофант"))))


;;; --- LEVEL 3: Правила модификатора времени ---

;; --- ЛОГИКА "МАСТЕРСТВО" ВО ВРЕМЕНИ ---
(defrule L3-skill-past
   (active-aspect (name "Мастерство") (time "Прошлое"))
   =>
   (assert (context-interpretation (text "В прошлом вы уже подобрали ключ к этой ситуации.") (time "Прошлое") (topic "Skill"))))

(defrule L3-skill-future
   (active-aspect (name "Мастерство") (time "Будущее"))
   =>
   (assert (context-interpretation (text "Вам придется проявить ловкость рук, чтобы открыть дверь.") (time "Будущее") (topic "Action"))))

;; --- ЛОГИКА "НЕИЗВЕСТНОСТЬ" ВО ВРЕМЕНИ ---
(defrule L3-chaos-present
   (active-aspect (name "Неизвестность") (time "Настоящее"))
   =>
   (assert (context-interpretation (text "Сейчас вы стоите на краю, старые карты не работают.") (time "Настоящее") (topic "Risk"))))

(defrule L3-chaos-past
   (active-aspect (name "Неизвестность") (time "Прошлое"))
   =>
   (assert (context-interpretation (text "Все началось со спонтанного, глупого решения.") (time "Прошлое") (topic "Origin"))))

;; --- ИНТЕРПРЕТАЦИЯ СИНТЕЗА "РИСКОВАННЫЙ СТАРТАП" (Маг+Шут) ---
(defrule L3-syn-startup-past
   (synthesis (meaning "Рискованный стартап") (times $? "Прошлое" $?))
   =>
   (assert (context-interpretation (text "Ваше начинание базировалось на чистом энтузиазме.") (time "Прошлое") (topic "Startup"))))

(defrule L3-syn-startup-future
   (synthesis (meaning "Рискованный стартап") (times $? "Будущее" $?))
   =>
   (assert (context-interpretation (text "Впереди новый проект, где нет гарантий, но есть свобода.") (time "Будущее") (topic "NewBiz"))))

;; --- ИНТЕРПРЕТАЦИЯ СИНТЕЗА "ПРОФЕССИОНАЛЬНОЕ УПРАВЛЕНИЕ" (Маг+Император) ---
(defrule L3-syn-mgmt-present
   (synthesis (meaning "Профессиональное управление") (times $? "Настоящее" $?))
   =>
   (assert (context-interpretation (text "Ситуация полностью под контролем, структура работает.") (time "Настоящее") (topic "Control"))))

;; --- ИНТЕРПРЕТАЦИЯ "ПЕРЕМЕНЫ" (КОЛЕСО) ---
(defrule L3-change-present
   (active-aspect (name "Перемены") (time "Настоящее"))
   =>
   (assert (context-interpretation (text "Дверь вращается прямо сейчас. Кто не успел, тот опоздал.") (time "Настоящее") (topic "Urgency"))))

(defrule L3-change-future
   (active-aspect (name "Перемены") (time "Будущее"))
   =>
   (assert (context-interpretation (text "Стабильности не ждите, готовьтесь к качке.") (time "Будущее") (topic "Warning"))))

;; --- ИНТЕРПРЕТАЦИЯ "СТОЙКОСТЬ" (СИЛА) ---
(defrule L3-strength-past
   (active-aspect (name "Стойкость") (time "Прошлое"))
   =>
   (assert (context-interpretation (text "Вы выдержали давление, и это ваш фундамент.") (time "Прошлое") (topic "Base"))))

;; --- ИНТЕРПРЕТАЦИЯ "ВЫБОР" (ВЛЮБЛЕННЫЕ) ---
(defrule L3-choice-future
   (active-aspect (name "Выбор") (time "Будущее"))
   =>
   (assert (context-interpretation (text "Скоро придется выбирать одну дверь из двух.") (time "Будущее") (topic "Decision"))))

;; --- ИНТЕРПРЕТАЦИЯ СИНТЕЗА "ИНТУИТИВНОЕ РЕШЕНИЕ" (Жрица+Влюбленные) ---
(defrule L3-syn-intuition-choice
   (synthesis (meaning "Интуитивное решение"))
   =>
   (assert (context-interpretation (text "Логика здесь бессильна, сердце знает верный вход.") (time "Общее") (topic "Heart"))))

;; --- ИНТЕРПРЕТАЦИЯ СИНТЕЗА "ВОЕННАЯ СТРАТЕГИЯ" (Император+Колесница) ---
(defrule L3-syn-war-future
   (synthesis (meaning "Военная стратегия") (times $? "Будущее" $?))
   =>
   (assert (context-interpretation (text "Вам предстоит захват новых территорий жесткими методами.") (time "Будущее") (topic "War"))))

;; --- ИНТЕРПРЕТАЦИЯ "ПОИСК" (ОТШЕЛЬНИК) ---
(defrule L3-search-present
   (active-aspect (name "Поиск") (time "Настоящее"))
   =>
   (assert (context-interpretation (text "Вы ищете дверь на ощупь в темноте.") (time "Настоящее") (topic "Lost"))))

;; --- ИНТЕРПРЕТАЦИЯ СИНТЕЗА "АДАПТАЦИЯ" (Маг+Колесо) ---
(defrule L3-syn-adapt-past
   (synthesis (meaning "Адаптация") (times $? "Прошлое" $?))
   =>
   (assert (context-interpretation (text "Вы удачно подстроились под прошлые изменения.") (time "Прошлое") (topic "Flexibility"))))

;; --- ИНТЕРПРЕТАЦИЯ СИНТЕЗА "СЕМЕЙНЫЕ ЦЕННОСТИ" (Императрица+Иерофант) ---
(defrule L3-syn-family
   (synthesis (meaning "Семейные ценности"))
   =>
   (assert (context-interpretation (text "Традиции и семья — ваша главная опора сейчас.") (time "Общее") (topic "Family"))))

;; --- ИНТЕРПРЕТАЦИЯ "ИЗОБИЛИЕ" (ИМПЕРАТРИЦА) ---
(defrule L3-abundance-future
   (active-aspect (name "Изобилие") (time "Будущее"))
   =>
   (assert (context-interpretation (text "Двери вашего дома будут открыты для гостей и плодов труда.") (time "Будущее") (topic "Harvest"))))

;; --- ИНТЕРПРЕТАЦИЯ СИНТЕЗА "БЕЗУМНАЯ ГОНКА" (Шут+Колесница) ---
(defrule L3-syn-race-present
   (synthesis (meaning "Безумная гонка") (times $? "Настоящее" $?))
   =>
   (assert (context-interpretation (text "События несутся с огромной скоростью без тормозов.") (time "Настоящее") (topic "Speed"))))

;; ЗАКОН / СПРАВЕДЛИВОСТЬ
(defrule L3-law-past
   (active-aspect (name "Закон") (time "Прошлое"))
   =>
   (assert (context-interpretation (text "Вы пожинаете плоды прошлых юридических решений.") (time "Прошлое") (topic "Legal"))))

(defrule L3-law-future
   (active-aspect (name "Закон") (time "Будущее"))
   =>
   (assert (context-interpretation (text "Вас ждет проверка или экзамен. Все тайное станет явным.") (time "Будущее") (topic "Exam"))))

;; ЖЕРТВА / ПОВЕШЕННЫЙ
(defrule L3-sacrifice-present
   (active-aspect (name "Жертва") (time "Настоящее"))
   =>
   (assert (context-interpretation (text "Сейчас дверь закрыта. Любое движение только затянет узлы.") (time "Настоящее") (topic "Stagnation"))))

;; ТРАНСФОРМАЦИЯ / СМЕРТЬ
(defrule L3-transform-future
   (active-aspect (name "Трансформация") (time "Будущее"))
   =>
   (assert (context-interpretation (text "Грядет полная зачистка. То, что отжило, уйдет навсегда.") (time "Будущее") (topic "EndGame"))))

(defrule L3-transform-past
   (active-aspect (name "Трансформация") (time "Прошлое"))
   =>
   (assert (context-interpretation (text "Вы пережили тяжелую утрату или резкую перемену.") (time "Прошлое") (topic "Trauma"))))

;; БАЛАНС / УМЕРЕННОСТЬ
(defrule L3-balance-present
   (active-aspect (name "Баланс") (time "Настоящее"))
   =>
   (assert (context-interpretation (text "Ситуация требует ювелирной точности и терпения.") (time "Настоящее") (topic "Patience"))))

;; ИСКУШЕНИЕ / ДЬЯВОЛ
(defrule L3-temptation-present
   (active-aspect (name "Искушение") (time "Настоящее"))
   =>
   (assert (context-interpretation (text "Вы находитесь в плену своих желаний или манипулятора.") (time "Настоящее") (topic "Trap"))))

;; КРАХ / БАШНЯ
(defrule L3-collapse-future
   (active-aspect (name "Крах") (time "Будущее"))
   =>
   (assert (context-interpretation (text "Ваши планы построены на песке и скоро рухнут.") (time "Будущее") (topic "Danger"))))

;; НАДЕЖДА / ЗВЕЗДА
(defrule L3-hope-future
   (active-aspect (name "Надежда") (time "Будущее"))
   =>
   (assert (context-interpretation (text "Впереди свет. Ваши мечты наконец обретут форму.") (time "Будущее") (topic "Inspiration"))))

;; ИЛЛЮЗИЯ / ЛУНА
(defrule L3-illusion-present
   (active-aspect (name "Иллюзия") (time "Настоящее"))
   =>
   (assert (context-interpretation (text "Вы блуждаете в тумане. Не верьте своим глазам.") (time "Настоящее") (topic "Deception"))))

;; ЯСНОСТЬ / СОЛНЦЕ
(defrule L3-clarity-present
   (active-aspect (name "Ясность") (time "Настоящее"))
   =>
   (assert (context-interpretation (text "Все предельно ясно. У вас карт-бланш на счастье.") (time "Настоящее") (topic "Success"))))

;; ВОЗРОЖДЕНИЕ / СУД
(defrule L3-rebirth-past
   (active-aspect (name "Возрождение") (time "Прошлое"))
   =>
   (assert (context-interpretation (text "Вы услышали зов и проснулись от долгого сна.") (time "Прошлое") (topic "Awakening"))))

;; ЗАВЕРШЕНИЕ / МИР
(defrule L3-completion-future
   (active-aspect (name "Завершение") (time "Будущее"))
   =>
   (assert (context-interpretation (text "Гештальт будет закрыт. Вы получите всё, к чему шли.") (time "Будущее") (topic "Achievement"))))

;; ИНТЕРПРЕТАЦИИ СИНТЕЗОВ
(defrule L3-syn-punishment
   (synthesis (meaning "Суровое наказание") (times $? "Будущее" $?))
   =>
   (assert (context-interpretation (text "Ошибки прошлого приведут к неминуемому краху.") (time "Будущее") (topic "Karma"))))

(defrule L3-syn-healing
   (synthesis (meaning "Исцеление") (times $? "Настоящее" $?))
   =>
   (assert (context-interpretation (text "Вы медленно, но верно восстанавливаете силы.") (time "Настоящее") (topic "Health"))))

(defrule L3-syn-obsession
   (synthesis (meaning "Самообман и зависимости"))
   =>
   (assert (context-interpretation (text "Вы добровольно вошли в клетку иллюзий.") (time "Общее") (topic "Addiction"))))

(defrule L3-syn-bankruptcy
   (synthesis (meaning "Банкротство"))
   =>
   (assert (context-interpretation (text "Ресурсы истощены, старая структура больше не кормит.") (time "Общее") (topic "Loss"))))


;;; --- LEVEL 4: Правила предсказаний ---

;; Совет для темы "Action" (Маг в будущем)
(defrule L4-adv-action
   (stage (value processing))
   (context-interpretation (topic "Action"))
   (not (final-advice))
   =>
   (assert (final-advice (text "Берите инициативу. Если ключа нет — создайте его своими руками."))))

;; Совет для темы "NewBiz" (Стартап в будущем)
(defrule L4-adv-newbiz
   (stage (value processing))
   (context-interpretation (topic "NewBiz"))
   (not (final-advice))
   =>
   (assert (final-advice (text "Смело прыгайте в новое дело. Неопределенность — ваш ресурс."))));

;; Совет для темы "Control" (Управление в настоящем)
(defrule L4-adv-control
   (stage (value processing))
   (context-interpretation (topic "Control"))
   (not (final-advice))
   =>
   (assert (final-advice (text "Не ослабляйте хватку. Структура и дисциплина гарантируют результат."))));

;; Совет для темы "Urgency" (Колесо в настоящем)
(defrule L4-adv-urgency
   (stage (value processing))
   (context-interpretation (topic "Urgency"))
   (not (final-advice))
   =>
   (assert (final-advice (text "Действуйте мгновенно! Окно возможностей закрывается."))));

;; Совет для темы "War" (Военная стратегия)
(defrule L4-adv-war
   (stage (value processing))
   (context-interpretation (topic "War"))
   (not (final-advice))
   =>
   (assert (final-advice (text "Отбросьте сантименты. Вижу цель — не вижу препятствий."))));

;; Совет для темы "Lost" (Отшельник сейчас)
(defrule L4-adv-lost
   (stage (value processing))
   (context-interpretation (topic "Lost"))
   (not (final-advice))
   =>
   (assert (final-advice (text "Остановитесь. Ответ не снаружи, а внутри вас. Нужна тишина."))));

;; Совет для темы "Decision" (Выбор в будущем)
(defrule L4-adv-decision
   (stage (value processing))
   (context-interpretation (topic "Decision"))
   (not (final-advice))
   =>
   (assert (final-advice (text "Взвесьте все 'за' и 'против', но выбирайте сердцем."))));

;; Совет для темы "Family"
(defrule L4-adv-family
   (stage (value processing))
   (context-interpretation (topic "Family"))
   (not (final-advice))
   =>
   (assert (final-advice (text "Обратитесь за поддержкой к роду или старшим наставникам."))));

;; Совет для темы "Harvest" (Изобилие)
(defrule L4-adv-harvest
   (stage (value processing))
   (context-interpretation (topic "Harvest"))
   (not (final-advice))
   =>
   (assert (final-advice (text "Будьте щедры. Чем больше отдаете, тем больше получаете."))));

;; Совет для темы "Speed" (Безумная гонка)
(defrule L4-adv-speed
   (stage (value processing))
   (context-interpretation (topic "Speed"))
   (not (final-advice))
   =>
   (assert (final-advice (text "Главное сейчас — не потерять управление. Смотрите только вперед."))));

;; Совет для темы "Heart" (Интуитивное решение)
(defrule L4-adv-heart
   (stage (value processing))
   (context-interpretation (topic "Heart"))
   (not (final-advice))
   =>
   (assert (final-advice (text "Доверьтесь первому впечатлению, оно самое верное."))));

(defrule L4-adv-stagnation
   (stage (value processing))
   (context-interpretation (topic "Stagnation"))
   (not (final-advice))
   =>
   (assert (final-advice (text "Не дергайтесь. Посмотрите на мир под другим углом и просто ждите."))))

(defrule L4-adv-endgame
   (stage (value processing))
   (context-interpretation (topic "EndGame"))
   (not (final-advice))
   =>
   (assert (final-advice (text "Отпустите прошлое без сожалений. Дверь должна закрыться, чтобы открылась новая."))))

(defrule L4-adv-trap
   (stage (value processing))
   (context-interpretation (topic "Trap"))
   (not (final-advice))
   =>
   (assert (final-advice (text "Срочно ищите выход. То, что кажется удовольствием, — это кандалы."))))

(defrule L4-adv-danger
   (stage (value processing))
   (context-interpretation (topic "Danger"))
   (not (final-advice))
   =>
   (assert (final-advice (text "Если фундамент гнилой — ломайте сами, пока это не сделала судьба."))))

(defrule L4-adv-success
   (stage (value processing))
   (context-interpretation (topic "Success"))
   (not (final-advice))
   =>
   (assert (final-advice (text "Наслаждайтесь моментом и делитесь теплом. Вы в центре внимания."))));

(defrule L4-adv-deception
   (stage (value processing))
   (context-interpretation (topic "Deception"))
   (not (final-advice))
   =>
   (assert (final-advice (text "Включите свет разума. Страхи преувеличены, а интуиция может подводить."))));

(defrule L4-adv-achievement
   (stage (value processing))
   (context-interpretation (topic "Achievement"))
   (not (final-advice))
   =>
   (assert (final-advice (text "Выходите на новый уровень. Мир у ваших ног, границы открыты."))));

(defrule L4-adv-patience
   (stage (value processing))
   (context-interpretation (topic "Patience"))
   (not (final-advice))
   =>
   (assert (final-advice (text "Смешивайте ингредиенты осторожно. Мера — ключ к успеху."))));

(defrule L4-adv-legal
   (stage (value processing))
   (context-interpretation (topic "Legal"))
   (not (final-advice))
   =>
   (assert (final-advice (text "Действуйте строго по закону и совести. Компромиссы сейчас опасны."))));

(defrule L4-adv-addiction
   (stage (value processing))
   (context-interpretation (topic "Addiction"))
   (not (final-advice))
   =>
   (assert (final-advice (text "Признайте свою зависимость. Это первый шаг к освобождению."))));

(defrule L4-adv-karma
   (stage (value processing))
   (context-interpretation (topic "Karma"))
   (not (final-advice))
   =>
   (assert (final-advice (text "Примите удар достойно. Это плата за вход в новую жизнь."))));


;; Default Advice (если ничего специфичного не сработало)
(defrule L4-fallback
   (declare (salience -10))
   (stage (value processing))
   (not (final-advice))
   =>
   (assert (final-advice (text "Будьте внимательны к знакам судьбы и доверяйте своей интуиции."))));

;; Универсальное правило: превращаем любую интерпретацию в совет, 
;; если еще нет финального совета.
(defrule general-interpretation-to-advice
   (stage (value processing))
   ;; Берем текст любой интерпретации
   (context-interpretation (text ?text))
   ;; Проверяем, что финального совета еще нет (чтобы не дублировать)
   (not (final-advice)) 
   =>
   (assert (final-advice (text ?text)))
)

;; ==============================================
;; ЭТАП 4: ОТЧЕТ (Вывод пользователю)
;; ==============================================

(defrule collection-done
   (declare (salience -20))
   ?st <- (stage (value processing))
   ;; Сразу считываем текущие сообщения в переменную $?old-msgs
   ?io <- (ioproxy (messages $?old-msgs))
   (final-advice (text ?advice))
   =>
   (bind ?msg (str-cat "Совет карт: " ?advice))
   
   (retract ?st)
   (assert (stage (value finished)))
   
   ;; Используем modify вместо slot-insert$
   ;; create$ создает новый список: старые сообщения + новое
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