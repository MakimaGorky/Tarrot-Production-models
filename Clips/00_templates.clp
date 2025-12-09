;;; 00_templates.clp
;;; Шаблоны данных для экспертной системы Таро

(deftemplate ioproxy
   "Связь с C#: сообщения для синтеза речи и ожидаемые ответы"
   (multislot messages) ; Что говорит робот
   (multislot answers)  ; Варианты ответов для пользователя
)

(deftemplate answer
   "Ответ пользователя, приходящий из C#"
   (slot text)
)

(deftemplate clearmessage
   "Сигнал для очистки буфера сообщений"
)

(deftemplate card
   "Описание карты Таро из базы знаний"
   (slot id)          ; Например: "0 Шут"
   (multislot aliases); Список для поиска ("шут" "дурак" "0")
   (slot aspect)      ; Например: "Свобода"
   (slot description) ; Текстовое описание
)

(deftemplate raw-input-card
   "Промежуточный факт: то, что ввел пользователь, но еще не привязано к ID"
   (slot user-text)     ; например "шут"
   (slot time-modifier) ; "Прошлое"
)

(deftemplate spread-slot
   "Выпавшая карта в конкретной позиции"
   (slot card-id)     ; Ссылка на ID карты
   (slot time-modifier) ; "Прошлое", "Настоящее", "Будущее"
)

;; Вспомогательный шаблон для удобства правил (промежуточный слой L1->L2)
(deftemplate active-aspect
   (slot name)          ; Имя аспекта
   (slot time)          ; Время (Прошлое/Настоящее/Будущее)
   (slot card-id)       ; От какой карты
)

(deftemplate synthesis
   "Уровень 2: Синтез"
   (slot meaning)       ; Сложное значение (напр. 'Контролируемый риск')
   (multislot times)    ; Список времен, участвовавших в создании (напр. "Прошлое" "Настоящее")
   (slot source)        ; Какие аспекты/карты собрали это
)

(deftemplate context-interpretation
   "Уровень 3: Интерпретация во времени"
   (slot text)
   (slot time)          ; К какому времени относится интерпретация
   (slot topic)         ; Тема (для связывания с советом)
)

(deftemplate final-advice
   "Уровень 4: Итоговый совет"
   (slot text)
)

(deftemplate stage
   "Текущее состояние диалога"
   (slot value) ; start, input-spread, processing, report, finished
)

(deftemplate deck
   "Вспомогательный факт для выбора случайных карт"
   (multislot card-ids)
)