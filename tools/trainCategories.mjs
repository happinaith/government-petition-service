import { torch } from 'js-pytorch';
import fs from 'node:fs';
import path from 'node:path';

// Категории и словарь должны совпадать с фронтом
const CATEGORIES = [
  'Экология',
  'Транспорт',
  'Образование',
  'Здравоохранение',
  'Инфраструктура',
  'Социальная поддержка',
];

const KEYWORDS = {
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

function textToVector(text) {
  const lower = text.toLowerCase();
  const vec = new Array(CATEGORIES.length).fill(0);
  for (const [key, weights] of Object.entries(KEYWORDS)) {
    if (lower.includes(key)) {
      for (let i = 0; i < vec.length; i++) {
        vec[i] += weights[i];
      }
    }
  }
  return torch.tensor(vec, { dtype: 'float32' });
}

// Примеры для обучения: текст -> индекс категории
// Можно расширять по желанию
const TRAIN_SAMPLES = [
  { text: 'В нашем районе переполненные мусорные свалки и грязь', cat: 'Экология' },
  { text: 'Просим убрать нелегальные свалки и навести порядок', cat: 'Экология' },
  { text: 'Требуем отремонтировать дорогу и добавить автобусные маршруты', cat: 'Транспорт' },
  { text: 'Не хватает общественного транспорта утром и вечером', cat: 'Транспорт' },
  { text: 'В школе не хватает учебников и учителей', cat: 'Образование' },
  { text: 'Просим построить новую школу в нашем районе', cat: 'Образование' },
  { text: 'В больнице не хватает врачей и оборудования', cat: 'Здравоохранение' },
  { text: 'Просим открыть новую поликлинику', cat: 'Здравоохранение' },
  { text: 'Нужно отремонтировать дороги и тротуары', cat: 'Инфраструктура' },
  { text: 'Просим построить детские площадки и парковки', cat: 'Инфраструктура' },
  { text: 'Просим повысить пенсии и социальные выплаты', cat: 'Социальная поддержка' },
  { text: 'Необходимо увеличить пособия малоимущим семьям', cat: 'Социальная поддержка' },
];

const catToIndex = Object.fromEntries(CATEGORIES.map((c, i) => [c, i]));

function buildDataset() {
  const xs = [];
  const ys = [];
  for (const sample of TRAIN_SAMPLES) {
    xs.push(textToVector(sample.text));
    ys.push(catToIndex[sample.cat]);
  }
  const X = torch.stack(xs); // [N, D]
  const y = torch.tensor(ys, { dtype: 'int64' }); // [N]
  return { X, y };
}

function train() {
  const { X, y } = buildDataset();
  const numCats = CATEGORIES.length;
  const dim = numCats; // вектор признаков той же длины

  let W = torch.randn([numCats, dim], { dtype: 'float32' }).mul(0.1);
  let b = torch.zeros([numCats], { dtype: 'float32' });

  const lr = 0.1;
  const epochs = 200;

  for (let epoch = 0; epoch < epochs; epoch++) {
    // прямой проход: y_pred = X @ W^T + b
    const logits = X.matmul(W.transpose(0, 1)).add(b);
    // кросс-энтропия
    const logSoftmax = logits.logSoftmax(1);
    const n = y.shape[0];
    const loss = logSoftmax.gather(1, y.reshape([n, 1])).neg().mean();

    // градиенты по W и b (простейший финитный шаг через autograd здесь не реализуем,
    // поэтому воспользуемся приближённым ручным градиентом для softmax-классификатора)
    const probs = logits.softmax(1);
    const yOneHot = torch.zerosLike(probs);
    for (let i = 0; i < n; i++) {
      yOneHot.data[i * numCats + y.data[i]] = 1;
    }
    const gradLogits = probs.sub(yOneHot).div(n);

    const gradW = gradLogits.transpose(0, 1).matmul(X); // [C,D]
    const gradb = gradLogits.sum(0);                    // [C]

    W = W.sub(gradW.mul(lr));
    b = b.sub(gradb.mul(lr));

    if ((epoch + 1) % 50 === 0) {
      console.log(`Epoch ${epoch + 1}, loss=${loss.item().toFixed(4)}`);
    }
  }

  return { W, b };
}

function main() {
  const { W, b } = train();
  const out = {
    categories: CATEGORIES,
    W: Array.from(W.data),
    b: Array.from(b.data),
  };

  const outPath = path.join(
    path.dirname(new URL(import.meta.url).pathname),
    '..',
    'petitionservice.client',
    'src',
    'ai',
    'category-weights.json',
  );

  fs.writeFileSync(outPath, JSON.stringify(out, null, 2), 'utf8');
  console.log('Saved weights to', outPath);
}

main();
