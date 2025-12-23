import re

# Файлы
INPUT_FILE = '02_rules.clp' 
OUTPUT_FILE = '02_rules-cf.clp'

# Функция CLIPS для расчета
CALC_FUNC = """
;;; FUNCTION: Расчет коэффициента уверенности
(deffunction calc-cf (?cf1 ?cf2 ?weight)
   (* (min ?cf1 ?cf2) ?weight)
)
"""

def process_l2_rule(rule_body):
    """
    УРОВЕНЬ 2: Вставляет (cf ?c1) и (cf ?c2) в начало фактов.
    """
    parts = rule_body.split('=>')
    if len(parts) != 2: return rule_body
    lhs, rhs = parts[0], parts[1]
    
    # 1. LHS: Заменяем (active-aspect ... на (active-aspect (cf ?c1) ...
    # Мы делаем это в два прохода, чтобы дать разные переменные (?c1 и ?c2)
    
    # Первый active-aspect -> ?c1
    lhs = re.sub(r'\(\s*active-aspect', '(active-aspect (cf ?c1)', lhs, count=1)
    
    # Второй active-aspect -> ?c2
    lhs = re.sub(r'\(\s*active-aspect', '(active-aspect (cf ?c2)', lhs, count=1)
    
    # 2. RHS: Вставляем расчет cf в начало synthesis
    # Было: (assert (synthesis (meaning...
    # Стало: (assert (synthesis (cf (calc-cf ?c1 ?c2 0.95)) (meaning...
    
    rhs = re.sub(r'\(\s*synthesis', '(synthesis (cf (calc-cf ?c1 ?c2 0.95))', rhs)
    
    return f"{lhs}=>{rhs}"

def process_l3_rule(rule_body):
    """
    УРОВЕНЬ 3: Вставляет (cf ?c) и пересчет (* ?c 0.9).
    """
    parts = rule_body.split('=>')
    if len(parts) != 2: return rule_body
    lhs, rhs = parts[0], parts[1]
    
    # 1. LHS: Ищем либо active-aspect, либо synthesis (в зависимости от правила)
    # Вставляем (cf ?c) в начало
    
    if 'active-aspect' in lhs:
        lhs = re.sub(r'\(\s*active-aspect', '(active-aspect (cf ?c)', lhs, count=1)
    elif 'synthesis' in lhs:
        lhs = re.sub(r'\(\s*synthesis', '(synthesis (cf ?c)', lhs, count=1)
        
    # 2. RHS: Вставляем cf в context-interpretation
    rhs = re.sub(r'\(\s*context-interpretation', '(context-interpretation (cf (* ?c 0.9))', rhs)
    
    return f"{lhs}=>{rhs}"

def main():
    try:
        with open(INPUT_FILE, 'r', encoding='utf-8') as f:
            content = f.read()
    except FileNotFoundError:
        print(f"Файл {INPUT_FILE} не найден!")
        return

    # Разбиваем на правила
    rules = re.split(r'(?=\(defrule)', content)
    
    new_blocks = []
    
    # Обработка заголовка/функции
    header = rules[0]
    if "(defrule" not in header:
        header += CALC_FUNC
        new_blocks.append(header)
        start_idx = 1
    else:
        new_blocks.append(CALC_FUNC)
        start_idx = 0

    for i in range(start_idx, len(rules)):
        block = rules[i]
        match = re.search(r'\(defrule\s+([\w-]+)', block)
        
        if match:
            name = match.group(1)
            if name.startswith("L2-"):
                block = process_l2_rule(block)
            elif name.startswith("L3-"):
                block = process_l3_rule(block)
        
        new_blocks.append(block)

    final = "".join(new_blocks)
    
    with open(OUTPUT_FILE, 'w', encoding='utf-8') as f:
        f.write(final)
        
    print(f"Готово. Файл сохранен как {OUTPUT_FILE}")
    print("Теперь структура будет корректной: (template (cf ?val) (slot val)...)")

if __name__ == '__main__':
    main()
    read()