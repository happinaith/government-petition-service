// Используем веса линейной модели, обученной оффлайн в tools/trainCategories.mjs с js-pytorch.
// Здесь только применяем готовые веса, чтобы классифицировать текст и предлагать заголовок.

import weights from './category-weights.json';

const CATEGORIES: string[] = weights.categories;
const NUM_CATS = CATEGORIES.length;

// Такой же словарь ключевых слов, как в оффлайн-скрипте обучения.
const KEYWORDS: Record<string, number[]> = {
  'экология': [1, 0, 0, 0, 0, 0],
  'мусор': [1, 0, 0, 0, 0, 0],
  'свалк': [1, 0, 0, 0, 0, 0],

  'дорог': [0, 1, 0, 0, 0, 0],
  'транспорт': [0, 1, 0, 0, 0, 0],
  'автобус': [0, 1, 0, 0, 0, 0],

  'школ': [0, 0, 1, 0, 0, 0],
  'образован': [0, 0, 1, 0, 0, 0],
  'университет': [0, 0, 1, 0, 0, 0],

  'больниц': [0, 0, 0, 1, 0, 0],
  'поликлиник': [0, 0, 0, 1, 0, 0],
  'медици': [0, 0, 0, 1, 0, 0],

  'дорогостро': [0, 0, 0, 0, 1, 0],
  'инфраструктур': [0, 0, 0, 0, 1, 0],

  'пенси': [0, 0, 0, 0, 0, 1],
  'пособи': [0, 0, 0, 0, 0, 1],
  'социальн': [0, 0, 0, 0, 0, 1],
};

function textToVector(text: string): number[] {
  const lower = text.toLowerCase();
  const vec = new Array(NUM_CATS).fill(0);

  for (const [key, weights] of Object.entries(KEYWORDS)) {
    if (lower.includes(key)) {
      for (let i = 0; i < vec.length; i++) {
        vec[i] += weights[i];
      }
    }
  }

  return vec;
}

function matVec(W: number[], x: number[]): number[] {
  // W хранится плоским массивом длины NUM_CATS * NUM_CATS
  const out = new Array(NUM_CATS).fill(0);
  for (let i = 0; i < NUM_CATS; i++) {
    let sum = 0;
    for (let j = 0; j < NUM_CATS; j++) {
      sum += W[i * NUM_CATS + j] * x[j];
    }
    out[i] = sum;
  }
  return out;
}

export function predictCategory(text: string): string {
  const x = textToVector(text);
  const y = matVec(weights.W, x).map((v, i) => v + weights.b[i]);

  let bestIdx = 0;
  let bestVal = y[0] ?? 0;
  for (let i = 1; i < y.length; i++) {
    if (y[i] > bestVal) {
      bestVal = y[i];
      bestIdx = i;
    }
  }

  if (bestVal === 0) {
    return 'Другое';
  }

  return CATEGORIES[bestIdx] ?? 'Другое';
}

export function suggestTitle(text: string): string {
  const category = predictCategory(text);

  switch (category) {
    case 'Экология':
      return 'Решение проблем экологии в нашем районе';
    case 'Транспорт':
      return 'Улучшение работы общественного транспорта';
    case 'Образование':
      return 'Развитие системы образования в нашем районе';
    case 'Здравоохранение':
      return 'Повышение качества медицинской помощи';
    case 'Инфраструктура':
      return 'Развитие городской инфраструктуры и благоустройства';
    case 'Социальная поддержка':
      return 'Усиление социальной поддержки граждан';
    default:
      return 'Предложение по улучшению жизни в городе';
  }
}
