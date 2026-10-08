using System;
using System.Collections.Generic;
using UnityEngine;

public enum Rarity { Comum, Raro, Epico, Lendario }

/// Definição de uma arma. As cartas multiplicam esses valores-base.
public class RunnerWeapon
{
    public string id;
    public string name;
    public float cooldown = 0.16f;  // segundos entre disparos
    public float damage = 1f;
    public float speed = 70f;       // velocidade relativa ao jogador
    public float life = 1.4f;       // duração do projétil (alcance)
    public float spread = 0f;       // abertura total em graus (para armas com vários projéteis)
    public float jitter = 0f;       // imprecisão aleatória em graus
    public float explode = 0f;      // raio de explosão
    public float size = 1f;
    public int pellets = 1;
    public int pierce = 0;
    public Color color = new Color(1f, 0.9f, 0.25f);

    // corpo a corpo: golpe em área na frente + "onda" curta (speed/life acima descrevem a onda)
    public bool melee;
    public float reach;             // alcance do golpe (metros à frente)
    public float arc;               // meia-largura do golpe (metros para cada lado)
    public string desc = "";

    /// Alcance aproximado em metros (projéteis) — para mostrar na tela.
    public float Range => speed * life;
}

public static class Weapons
{
    // ---------------- corpo a corpo
    public static RunnerWeapon Sword() => new RunnerWeapon
    {
        id = "espada", name = "Espada", melee = true, cooldown = 0.38f, damage = 2f, reach = 4f, arc = 1.6f,
        speed = 42f, life = 0.5f, size = 1f, color = new Color(0.85f, 0.92f, 1f)
    };

    public static RunnerWeapon Spear() => new RunnerWeapon
    {
        id = "lanca", name = "Lança", melee = true, cooldown = 0.5f, damage = 2.6f, reach = 6.5f, arc = 0.8f,
        speed = 50f, life = 0.5f, pierce = 2, size = 0.8f, color = new Color(1f, 0.8f, 0.4f)
    };

    public static RunnerWeapon Hammer() => new RunnerWeapon
    {
        id = "martelo", name = "Martelo de Guerra", melee = true, cooldown = 0.85f, damage = 4.5f, reach = 3.2f, arc = 3.6f,
        speed = 40f, life = 0.5f, explode = 2f, size = 1.4f, color = new Color(1f, 0.55f, 0.2f)
    };

    // ---------------- à distância (alcance reduzido; só armas de longo alcance vão longe)
    public static RunnerWeapon Pistol() => new RunnerWeapon
    {
        id = "pistola", name = "Pistola", cooldown = 0.16f, damage = 1f, speed = 60f, life = 0.6f
    };

    public static RunnerWeapon Shotgun() => new RunnerWeapon
    {
        id = "escopeta", name = "Escopeta", cooldown = 0.55f, damage = 0.8f, speed = 55f, life = 0.3f,
        pellets = 6, spread = 14f, jitter = 2f, size = 0.85f, color = new Color(1f, 0.6f, 0.2f)
    };

    public static RunnerWeapon Minigun() => new RunnerWeapon
    {
        id = "metralhadora", name = "Metralhadora", cooldown = 0.06f, damage = 0.45f, speed = 70f, life = 0.5f,
        jitter = 2.5f, size = 0.75f, color = new Color(1f, 1f, 0.5f)
    };

    public static RunnerWeapon DualPistols() => new RunnerWeapon
    {
        id = "duplas", name = "Pistolas Duplas", cooldown = 0.13f, damage = 0.8f, speed = 65f, life = 0.55f,
        pellets = 2, spread = 4f, color = new Color(0.5f, 1f, 0.9f)
    };

    public static RunnerWeapon Sling() => new RunnerWeapon
    {
        id = "funda_arma", name = "Funda", cooldown = 0.32f, damage = 1.7f, speed = 55f, life = 0.7f,
        size = 0.9f, color = new Color(0.8f, 0.78f, 0.72f)
    };

    public static RunnerWeapon Bazooka() => new RunnerWeapon
    {
        id = "bazuca", name = "Bazuca", cooldown = 0.8f, damage = 3f, speed = 40f, life = 1.0f,
        explode = 3.5f, size = 1.7f, color = new Color(1f, 0.35f, 0.15f)
    };

    // longo alcance
    public static RunnerWeapon Railgun() => new RunnerWeapon
    {
        id = "railgun", name = "Railgun", cooldown = 0.7f, damage = 4.5f, speed = 150f, life = 0.6f,
        pierce = 5, size = 1.4f, color = new Color(0.4f, 0.8f, 1f)
    };

    public static RunnerWeapon Longbow() => new RunnerWeapon
    {
        id = "arco", name = "Arco Longo", cooldown = 0.45f, damage = 2.2f, speed = 80f, life = 1.1f,
        pierce = 1, size = 0.9f, color = new Color(0.75f, 1f, 0.55f)
    };
}

/// Todos os atributos que as cartas podem alterar durante uma jogatina.
public class RunnerStats
{
    public RunnerWeapon weapon = Weapons.Sword();   // começa com a espada

    // arma
    public float damageMul = 1f;
    public float fireRateMul = 1f;
    public float bulletSpeedMul = 1f;
    public float sizeMul = 1f;
    public int extraProjectiles;
    public int extraPierce;
    public float explodeBonus;
    public float critChance = 0.05f;
    public float critMul = 2f;
    public float homing;
    public int chain;

    // jogador
    public float jumpMul = 1f;
    public float laneMul = 1f;
    public bool doubleJump;
    public float invulnTime = 1.5f;
    public int shieldLevel;
    public int vampKills;           // 0 = desligado; senão cura 1 vida a cada N abates
    public int drones;
    public bool bulletTime;
    public float bulletTimeCooldown = 15f;
    public float killExplodeChance;
    public bool retaliation;
    public bool ram;

    // bênçãos (cartas novas)
    public int orbitBlades;          // Lâminas Giratórias
    public int stompLevel;           // Pisão do Querubim
    public bool mana;                // Maná do Céu
    public int slingLevel;           // Funda de Davi
    public bool trumpets;            // Trombetas de Jericó
    public bool redSea;              // Abrir o Mar
    public int dailyBread;           // Pão Diário
    public int skyFire;              // Fogo do Céu
    public int wrathLevel;           // Ira Justa
    public bool mosesStaff;          // Vara de Moisés
    public float bulletTimeDuration = 3f;
    public float cardStepMul = 1f;

    // cartas que mudam as regras
    public float gravityMul = 1f;          // Dilúvio: você flutua
    public bool flood;                     // Arca de Noé (Dilúvio)
    public bool jacobLadder;               // Escada de Jacó: pisa em quase tudo
    public bool gideon;                    // Os 300 de Gideão: baralho de 10, cartas valem 2x
    public bool josephDreams;              // Sonhos de José: vê as próximas cartas
    public bool babelConfusion;            // Confusão de Babel
    public bool lotsWife;                  // Mulher de Ló

    // cartas novas
    public float siclosMul = 1f;           // Ouro de Ofir / Rede de Pedro
    public float igniteChance;             // Brasas do Altar
    public float wetChance;                // Orvalho de Gideão
    public bool elijahMantle;              // Manto de Elias

    // evoluções
    public float chainMul = 1f;            // multiplica o dano do raio em cadeia
    public float mosesChance = 0.08f;
    public bool judgmentTrumpet;           // Trombeta do Juízo
    public bool manaEternal;               // Maná Eterno
    public bool pillarOfFire;              // Coluna de Fogo e Nuvem
    public bool resurrection;              // Ressurreição (uma vez)
    public bool resurrectionUsed;
    public bool aaronRod;                  // Vara de Arão

    // jogo
    public float scoreMul = 1f;
    public float runSpeedMul = 1f;
    public int rerolls = 1;
    public int choices = 3;

    // valores finais
    public float Damage => weapon.damage * damageMul;
    public float Cooldown => Mathf.Max(0.035f, weapon.cooldown / fireRateMul);
    public int Pellets => weapon.pellets + extraProjectiles;
    public int Pierce => weapon.pierce + extraPierce;
    public float BulletSpeed => weapon.speed * bulletSpeedMul;
    public float BulletLife => weapon.life * (1f + (bulletSpeedMul - 1f) * 0.6f);
    public float Size => weapon.size * sizeMul;
    public bool Melee => weapon.melee;
    public float Reach => weapon.reach * (1f + (sizeMul - 1f) * 0.5f) * (1f + (bulletSpeedMul - 1f) * 0.3f);
    public float MeleeHalfWidth => weapon.arc * (1f + (sizeMul - 1f) * 0.5f);
    public float Range => BulletSpeed * BulletLife;
    public float Explode => weapon.explode + (explodeBonus > 0f ? explodeBonus + (weapon.explode > 0f ? 0f : 0.5f) : 0f);
    /// Estimativa grosseira do dano por segundo da build (usada para dar vida aos chefes).
    public float EstimatedDps
    {
        get
        {
            float pelletsHit = 1f + (Pellets - 1) * (weapon.spread > 0f ? 0.45f : 0.6f);
            float dps = Damage * pelletsHit / Cooldown;
            if (Melee) dps *= 0.8f;   // golpe forte + onda de 60%, mas só acerta de perto
            dps *= 1f + critChance * (critMul - 1f);
            if (Explode > 0f) dps *= 1.2f;
            dps += drones * 0.7f * damageMul / 0.9f;
            return dps;
        }
    }

    public float ShieldRecharge => shieldLevel <= 0 ? 0f : 15f / (1f + (shieldLevel - 1) * 0.6f);
}

public class RunnerCard
{
    public string id;
    public string name;
    public string desc;
    public Rarity rarity;
    public int maxStacks = 1;
    public bool curse;
    public bool isWeapon;
    public bool isEvolution;
    public bool plague;             // praga: entra no baralho por eventos e atrapalha
    public string reqText = "";     // "Espada + Lâminas Giratórias" (evoluções)
    public Func<RunnerGame, bool> cond;
    public Action<RunnerGame> apply;
}

public static class CardDB
{
    static List<RunnerCard> all;
    public static List<RunnerCard> All => all ?? (all = Build());

    static RunnerCard C(string id, string name, Rarity r, int max, string desc, Action<RunnerGame> apply,
        Func<RunnerGame, bool> cond = null, bool curse = false)
    {
        return new RunnerCard { id = id, name = name, rarity = r, maxStacks = max, desc = desc, apply = apply, cond = cond, curse = curse };
    }

    static RunnerCard W(string id, string name, Rarity r, string desc, Func<RunnerWeapon> make)
    {
        return new RunnerCard
        {
            id = "arma_" + id, name = name, rarity = r, maxStacks = 99, desc = desc, isWeapon = true,
            cond = g => g.stats.weapon.id != id,
            apply = g => g.stats.weapon = make()
        };
    }

    static List<RunnerCard> Build()
    {
        var l = new List<RunnerCard>();

        // ---------------- COMUNS
        l.Add(C("dano", "Munição Pesada", Rarity.Comum, 10, "+20% de dano.", g => g.stats.damageMul += 0.2f));
        l.Add(C("cadencia", "Gatilho Leve", Rarity.Comum, 10, "+15% de cadência de tiro.", g => g.stats.fireRateMul += 0.15f));
        l.Add(C("polvora", "Pólvora Extra", Rarity.Comum, 5, "+25% de velocidade e alcance dos projéteis.", g => g.stats.bulletSpeedMul += 0.25f));
        l.Add(C("calibre", "Calibre Grosso", Rarity.Comum, 5, "Projéteis 30% maiores e +10% de dano.", g => { g.stats.sizeMul += 0.3f; g.stats.damageMul += 0.1f; }));
        l.Add(C("coracao", "Coração Extra", Rarity.Comum, 6, "+1 vida máxima e recupera 1 vida.", g => { g.maxLives++; g.Heal(1); }));
        l.Add(C("agil", "Pernas Ágeis", Rarity.Comum, 4, "Troca de faixa 30% mais rápida e pulo 10% mais alto.", g => { g.stats.laneMul += 0.3f; g.stats.jumpMul += 0.1f; }));
        l.Add(C("sorte", "Trevo da Sorte", Rarity.Comum, 3, "+1 rerrolagem de cartas por escolha.", g => g.stats.rerolls++));
        l.Add(C("crit", "Olho de Águia", Rarity.Comum, 6, "+10% de chance de acerto crítico.", g => g.stats.critChance += 0.1f));
        l.Add(C("ganancia", "Ganância", Rarity.Comum, 5, "+25% de pontos ganhos.", g => g.stats.scoreMul += 0.25f));
        l.Add(C("fantasma", "Reflexos", Rarity.Comum, 3, "+0,5s de invencibilidade após tomar dano.", g => g.stats.invulnTime += 0.5f));

        // ---------------- RAROS
        l.Add(C("multi", "Tiro Múltiplo", Rarity.Raro, 6, "+1 projétil por disparo.", g => g.stats.extraProjectiles++));
        l.Add(C("perfura", "Perfurante", Rarity.Raro, 5, "Projéteis atravessam +1 inimigo.", g => g.stats.extraPierce++));
        l.Add(C("explosivo", "Munição Explosiva", Rarity.Raro, 4, "Projéteis explodem ao acertar (+1,5 de raio).", g => g.stats.explodeBonus += 1.5f));
        l.Add(C("homing", "Teleguiado", Rarity.Raro, 3, "Projéteis perseguem o inimigo mais próximo.", g => g.stats.homing += 1f));
        l.Add(C("escudo", "Escudo de Energia", Rarity.Raro, 3, "Bloqueia 1 dano e recarrega com o tempo. Acumular recarrega mais rápido.", g => { g.stats.shieldLevel++; g.RechargeShieldNow(); }));
        l.Add(C("pulo2", "Pulo Duplo", Rarity.Raro, 1, "Permite pular mais uma vez no ar.", g => g.stats.doubleJump = true));
        l.Add(C("vampiro", "Vampirismo", Rarity.Raro, 3, "Recupera 1 vida a cada 30 abates (20, depois 12).", g => g.stats.vampKills = g.stats.vampKills == 0 ? 30 : (g.stats.vampKills == 30 ? 20 : 12)));
        l.Add(C("fatal", "Golpe Fatal", Rarity.Raro, 4, "+75% de dano crítico.", g => g.stats.critMul += 0.75f));
        l.Add(C("kit", "Kit Médico", Rarity.Raro, 99, "Recupera todas as vidas.", g => g.Heal(99), g => g.lives < g.maxLives));

        // ---------------- ARMAS (corpo a corpo)
        l.Add(W("espada", "Arma: Espada", Rarity.Comum, "CORPO A CORPO. Golpe rápido na sua faixa + onda de luz curta. Bloqueia bolas de fogo.", Weapons.Sword));
        l.Add(W("lanca", "Arma: Lança", Rarity.Raro, "CORPO A CORPO. Estocada longa e estreita que atravessa inimigos.", Weapons.Spear));
        l.Add(W("martelo", "Arma: Martelo de Guerra", Rarity.Raro, "CORPO A CORPO. Lento, mas acerta as 3 faixas e explode.", Weapons.Hammer));
        // ---------------- ARMAS (à distância)
        l.Add(W("pistola", "Arma: Pistola", Rarity.Comum, "Tiro simples de médio alcance (~36 m).", Weapons.Pistol));
        l.Add(W("escopeta", "Arma: Escopeta", Rarity.Raro, "Leque de 6 chumbos de CURTO alcance (~16 m). Devastadora de perto.", Weapons.Shotgun));
        l.Add(W("metralhadora", "Arma: Metralhadora", Rarity.Raro, "Cadência altíssima, dano baixo, médio alcance (~35 m).", Weapons.Minigun));
        l.Add(W("duplas", "Arma: Pistolas Duplas", Rarity.Raro, "Dois tiros por disparo, médio alcance (~36 m).", Weapons.DualPistols));
        l.Add(W("bazuca", "Arma: Bazuca", Rarity.Epico, "Foguetes lentos com grande explosão (~40 m).", Weapons.Bazooka));
        // ---------------- ARMAS (longo alcance)
        l.Add(W("arco", "Arma: Arco Longo", Rarity.Raro, "LONGO ALCANCE (~88 m). Flechas fortes que atravessam 1 inimigo.", Weapons.Longbow));
        l.Add(W("railgun", "Arma: Railgun", Rarity.Epico, "LONGO ALCANCE (~90 m). Disparo muito forte que atravessa 5 inimigos.", Weapons.Railgun));

        // ---------------- ÉPICOS
        l.Add(C("corrente", "Corrente Elétrica", Rarity.Epico, 3, "Acertos soltam um raio em +2 inimigos próximos (50% do dano).", g => g.stats.chain += 2));
        l.Add(C("drone", "Drone de Combate", Rarity.Epico, 4, "Um drone te acompanha e atira sozinho nos inimigos.", g => { g.stats.drones++; g.AddDrone(); }));
        l.Add(C("reacao", "Reação em Cadeia", Rarity.Epico, 3, "+25% de chance de inimigos explodirem ao morrer.", g => g.stats.killExplodeChance += 0.25f));
        l.Add(C("represalia", "Represália", Rarity.Epico, 1, "Ao tomar dano, uma onda de choque destrói os inimigos próximos.", g => g.stats.retaliation = true));
        l.Add(C("adrenalina", "Adrenalina", Rarity.Epico, 3, "Corre 12% mais rápido, +15% de cadência e +40% de pontos.", g => { g.stats.runSpeedMul += 0.12f; g.stats.fireRateMul += 0.15f; g.stats.scoreMul += 0.4f; }));
        l.Add(C("vidro", "Canhão de Vidro", Rarity.Epico, 1, "MALDIÇÃO: dano x2, mas -2 vidas máximas (mínimo 1).", g =>
        {
            g.stats.damageMul *= 2f;
            g.maxLives = Mathf.Max(1, g.maxLives - 2);
            g.lives = Mathf.Min(g.lives, g.maxLives);
        }, null, true));
        l.Add(C("pacto", "Pacto de Sangue", Rarity.Epico, 2, "MALDIÇÃO: +2 projéteis, mas -1 vida máxima.", g =>
        {
            g.stats.extraProjectiles += 2;
            g.maxLives = Mathf.Max(1, g.maxLives - 1);
            g.lives = Mathf.Min(g.lives, g.maxLives);
        }, g => g.maxLives > 1, true));

        // ---------------- BÊNÇÃOS (novas)
        l.Add(C("pao", "Pão Diário", Rarity.Comum, 3, "Recupera 1 vida sempre que você sobe de nível. (Mt 6:11)", g => g.stats.dailyBread++));
        l.Add(C("dizimo", "Dízimo", Rarity.Comum, 3, "+10% de pontos e as próximas cartas chegam 10% mais cedo. (Ml 3:10)", g => { g.stats.scoreMul += 0.1f; g.stats.cardStepMul *= 0.9f; }));
        l.Add(C("funda", "Funda de Davi", Rarity.Raro, 3, "+35% de dano contra elites, lamassus e chefes. (1Sm 17)", g => g.stats.slingLevel++));
        l.Add(C("mana", "Maná do Céu", Rarity.Raro, 1, "A cada 15 abates você ganha +5% de dano permanente. (Êx 16)", g => g.stats.mana = true));
        l.Add(C("pisao", "Pisão do Querubim", Rarity.Raro, 3, "Ao cair de um pulo, uma onda de choque fere tudo em volta.", g => g.stats.stompLevel++));
        l.Add(C("setimo", "Sétimo Dia", Rarity.Raro, 2, "Tempo Bala dura +1,5s e recarrega 30% mais rápido. (Gn 2:2)", g => { g.stats.bulletTimeDuration += 1.5f; g.stats.bulletTimeCooldown *= 0.7f; }, g => g.stats.bulletTime));
        l.Add(C("laminas", "Lâminas Giratórias", Rarity.Epico, 3, "Espadas flamejantes giram ao seu redor, ferindo inimigos e apagando bolas de fogo. (Gn 3:24)", g => { g.stats.orbitBlades++; g.RefreshOrbitBlades(); }));
        l.Add(C("jerico", "Trombetas de Jericó", Rarity.Epico, 1, "A cada 12s as muralhas e barreiras à frente desabam. (Js 6)", g => g.stats.trumpets = true));
        l.Add(C("fogoceu", "Fogo do Céu", Rarity.Epico, 3, "A cada 6s cai fogo do céu sobre um inimigo à frente (dano x4). Acumular deixa mais frequente. (1Rs 18)", g => g.stats.skyFire++));
        l.Add(C("ira", "Ira Justa", Rarity.Epico, 3, "+12% de dano para cada vida que estiver faltando.", g => g.stats.wrathLevel++));

        // ---------------- LENDÁRIOS
        l.Add(C("marvermelho", "Abrir o Mar", Rarity.Lendario, 1, "Ao tomar dano, o caminho se abre: tudo nos próximos 45 m é varrido. (Êx 14)", g => g.stats.redSea = true));
        l.Add(C("vara", "Vara de Moisés", Rarity.Lendario, 1, "Cada acerto tem 8% de chance de reduzir o inimigo a pó (exceto chefes). (Êx 7)", g => g.stats.mosesStaff = true));
        l.Add(C("tempo", "Tempo Bala", Rarity.Lendario, 1, "Shift / Q: desacelera o tempo por 3s (recarga 15s). Você continua rápido.", g => g.stats.bulletTime = true));
        l.Add(C("ariete", "Armadura de Aríete", Rarity.Lendario, 1, "Encostar em alvos, torretas e tiros inimigos destrói eles sem te machucar.", g => g.stats.ram = true));
        l.Add(C("destino", "Visão do Destino", Rarity.Lendario, 1, "+1 carta para escolher em cada nível.", g => g.stats.choices++));
        l.Add(C("arsenal", "Arsenal Supremo", Rarity.Lendario, 1, "Dano x1,5, cadência +30% e +1 projétil.", g => { g.stats.damageMul *= 1.5f; g.stats.fireRateMul += 0.3f; g.stats.extraProjectiles++; }));

        // ---------------- CARTAS NOVAS (variedade)
        l.Add(C("lampada", "Lâmpada aos Pés", Rarity.Comum, 4, "+15% de alcance dos projéteis e +5% de crítico. (Sl 119:105)", g => { g.stats.bulletSpeedMul += 0.15f; g.stats.critChance += 0.05f; }));
        l.Add(C("sal", "Sal da Terra", Rarity.Comum, 5, "+12% de dano e +10% de pontos. (Mt 5:13)", g => { g.stats.damageMul += 0.12f; g.stats.scoreMul += 0.1f; }));
        l.Add(C("cinto", "Cinto da Verdade", Rarity.Comum, 3, "+0,4s de invencibilidade após dano e +6% de dano. (Ef 6:14)", g => { g.stats.invulnTime += 0.4f; g.stats.damageMul += 0.06f; }));
        l.Add(C("espigas", "Espigas no Sábado", Rarity.Comum, 6, "+12% de cadência de tiro. (Mc 2:23)", g => g.stats.fireRateMul += 0.12f));
        l.Add(C("talento", "Parábola dos Talentos", Rarity.Comum, 4, "+30% de pontos ganhos. (Mt 25:21)", g => g.stats.scoreMul += 0.3f));
        l.Add(C("cajado_pastor", "Cajado do Pastor", Rarity.Comum, 3, "Troca de faixa 25% mais rápida e pulo 10% mais alto. (Sl 23:4)", g => { g.stats.laneMul += 0.25f; g.stats.jumpMul += 0.1f; }));
        l.Add(C("sopro", "Sopro de Vida", Rarity.Raro, 2, "+1 vida máxima e +1 nível de escudo. (Gn 2:7)", g => { g.maxLives++; g.Heal(1); g.stats.shieldLevel++; g.RechargeShieldNow(); }));
        l.Add(C("aguia", "Asas de Águia", Rarity.Raro, 1, "Pulo duplo e pulos 15% mais altos. (Is 40:31)", g => { g.stats.doubleJump = true; g.stats.jumpMul += 0.15f; }));
        l.Add(C("ouro_ofir", "Ouro de Ofir", Rarity.Raro, 3, "+25% de siclos coletados. (1Rs 9:28)", g => g.stats.siclosMul += 0.25f));
        l.Add(C("chifre_oleo", "Chifre de Azeite", Rarity.Raro, 3, "+8% de crítico e +20% de dano crítico. (1Sm 16:13)", g => { g.stats.critChance += 0.08f; g.stats.critMul += 0.2f; }));
        l.Add(C("brasas", "Brasas do Altar", Rarity.Raro, 3, "Acertos têm +15% de chance de INCENDIAR o inimigo. (Is 6:6)", g => g.stats.igniteChance += 0.15f));
        l.Add(C("orvalho", "Orvalho do Velo", Rarity.Raro, 3, "Acertos têm +20% de chance de MOLHAR o inimigo. (Jz 6:38)", g => g.stats.wetChance += 0.2f));
        l.Add(C("relampago", "Relâmpagos do Sinai", Rarity.Raro, 3, "Raio em cadeia em +1 inimigo e +20% de dano do raio. (Êx 19:16)", g => { g.stats.chain += 1; g.stats.chainMul *= 1.2f; }));
        l.Add(C("anjo_guarda", "Anjo da Guarda", Rarity.Epico, 2, "Um drone celestial te acompanha e +1 vida máxima. (Sl 91:11)", g => { g.stats.drones++; g.AddDrone(); g.maxLives++; g.Heal(1); }));
        l.Add(C("forca_sansao", "Força de Sansão", Rarity.Epico, 2, "+40% de dano com armas corpo a corpo (+15% com as outras). (Jz 15:15)", g => g.stats.damageMul += g.stats.Melee ? 0.4f : 0.15f));
        l.Add(C("granizo", "Granizo de Fogo", Rarity.Epico, 2, "Fogo do céu cai sobre os inimigos com frequência. (Êx 9:24)", g => g.stats.skyFire++));
        l.Add(C("rede_pedro", "Rede de Pedro", Rarity.Epico, 1, "+50% de siclos coletados e +1 rerrolagem. (Lc 5:6)", g => { g.stats.siclosMul += 0.5f; g.stats.rerolls++; }));
        l.Add(C("porcao", "Porção Dobrada", Rarity.Epico, 2, "+1 rerrolagem e +10% de dano. (2Rs 2:9)", g => { g.stats.rerolls++; g.stats.damageMul += 0.1f; }));
        l.Add(C("querubins", "Rodas de Ezequiel", Rarity.Lendario, 1, "Duas rodas cheias de olhos giram ao seu redor e +20% de cadência. (Ez 1:16)", g => { g.stats.orbitBlades += 2; g.RefreshOrbitBlades(); g.stats.fireRateMul += 0.2f; }));
        l.Add(C("manto", "Manto de Elias", Rarity.Lendario, 1, "Ao tomar dano, cai fogo do céu 3 vezes sobre os inimigos à frente. (2Rs 2:13)", g => g.stats.elijahMantle = true));
        l.Add(C("leao", "Leão de Judá", Rarity.Lendario, 1, "Dano x1,5 e +2 vidas máximas, mas troca de faixa 20% mais lenta. (Ap 5:5)", g => { g.stats.damageMul *= 1.5f; g.maxLives += 2; g.Heal(2); g.stats.laneMul -= 0.2f; }));

        // ---------------- CARTAS QUE MUDAM AS REGRAS
        l.Add(C("diluvio", "Arca de Noé", Rarity.Lendario, 1, "O DILÚVIO: muralhas e barreiras afundam, você flutua (pulos longos) e todo inimigo fica molhado. (Gn 7:17)", g => g.StartFlood()));
        l.Add(C("jaco", "Escada de Jacó", Rarity.Lendario, 1, "Pulos 60% mais altos e você pode pisar em qualquer inimigo ou muralha que não seja indestrutível. (Gn 28:12)", g => { g.stats.jumpMul += 0.6f; g.stats.jacobLadder = true; }));
        l.Add(C("gideao", "Os 300 de Gideão", Rarity.Lendario, 1, "Seu baralho desta jornada fica com só 10 cartas, mas toda carta escolhida vale em dobro. (Jz 7:7)", g => g.StartGideon()));
        l.Add(C("babel", "Confusão de Babel", Rarity.Lendario, 1, "Seus acertos têm 20% de chance de confundir: o inimigo confuso ataca os outros. (Gn 11:7)", g => g.stats.babelConfusion = true));
        l.Add(C("jose", "Sonhos de José", Rarity.Epico, 1, "Você vê as próximas 3 cartas do baralho e ganha +2 rerrolagens. (Gn 41:25)", g => { g.stats.josephDreams = true; g.stats.rerolls += 2; }));
        l.Add(C("fartura", "Sete Anos de Fartura", Rarity.Epico, 1, "Os próximos 7 níveis oferecem 5 cartas. Depois vêm 7 níveis de fome, com só 1. (Gn 41:29)", g => g.StartPlenty()));
        l.Add(C("lo", "Mulher de Ló", Rarity.Epico, 1, "MALDIÇÃO: dano x2, mas não olhe para trás — voltar para a faixa de onde acabou de sair custa 1 vida. (Gn 19:26)", g => { g.stats.damageMul *= 2f; g.stats.lotsWife = true; }, null, true));

        // ---------------- PRAGAS (só entram no baralho por eventos, caminhos e juramentos)
        l.Add(Plague("praga_ras", "Praga das Rãs", "Não pode ser escolhida: só ocupa espaço na escolha. Queime no altar. (Êx 8:6)"));
        l.Add(Plague("praga_gafanhotos", "Praga de Gafanhotos", "Ao aparecer, devora 5 siclos. Queime no altar. (Êx 10:15)"));
        l.Add(Plague("idolo", "Ídolo de Ouro", "Enquanto estiver no seu baralho: -10% de dano. Queime no altar. (Êx 32:4)"));

        return l;
    }

    static RunnerCard Plague(string id, string name, string desc)
        => new RunnerCard { id = id, name = name, rarity = Rarity.Comum, maxStacks = 99, desc = "PRAGA: " + desc, curse = true, plague = true, apply = g => { } };

    public static readonly Color EvolutionColor = new Color(1f, 0.95f, 0.7f);

    public static Color RarityColor(Rarity r)
    {
        switch (r)
        {
            case Rarity.Raro: return new Color(0.3f, 0.6f, 1f);
            case Rarity.Epico: return new Color(0.75f, 0.35f, 1f);
            case Rarity.Lendario: return new Color(1f, 0.6f, 0.1f);
            default: return new Color(0.85f, 0.85f, 0.85f);
        }
    }

    public static string RarityName(Rarity r)
    {
        switch (r)
        {
            case Rarity.Raro: return "RARA";
            case Rarity.Epico: return "ÉPICA";
            case Rarity.Lendario: return "LENDÁRIA";
            default: return "COMUM";
        }
    }
}

public enum Family { Nenhuma, Fogo, Agua, Guerra, Fe, Sinais }

/// <summary>
/// Famílias de cartas: juntar 3 e 5 cartas diferentes da mesma família liga bônus extras.
/// </summary>
public static class Families
{
    public static readonly Family[] All = { Family.Fogo, Family.Agua, Family.Guerra, Family.Fe, Family.Sinais };

    static Dictionary<string, Family> map;
    static Dictionary<string, Family> Map
    {
        get
        {
            if (map != null) return map;
            map = new Dictionary<string, Family>();
            System.Action<Family, string[]> add = (f, ids) => { foreach (var id in ids) map[id] = f; };
            add(Family.Fogo, new[] { "polvora", "calibre", "explosivo", "reacao", "adrenalina", "vidro", "laminas", "fogoceu", "ira", "lo", "brasas", "granizo", "manto" });
            add(Family.Agua, new[] { "coracao", "pao", "dizimo", "kit", "vampiro", "mana", "marvermelho", "diluvio", "fartura", "sal", "orvalho", "rede_pedro", "sopro" });
            add(Family.Guerra, new[] { "dano", "cadencia", "crit", "multi", "perfura", "homing", "fatal", "funda", "pacto", "arsenal", "gideao", "espigas", "chifre_oleo", "forca_sansao", "leao" });
            add(Family.Fe, new[] { "agil", "fantasma", "escudo", "pulo2", "setimo", "represalia", "ariete", "jaco", "cinto", "cajado_pastor", "aguia", "anjo_guarda" });
            add(Family.Sinais, new[] { "sorte", "ganancia", "pisao", "corrente", "drone", "jerico", "vara", "tempo", "destino", "babel", "jose", "lampada", "talento", "ouro_ofir", "relampago", "porcao", "querubins" });
            return map;
        }
    }

    public static Family Of(RunnerCard c)
    {
        Family f;
        return c != null && Map.TryGetValue(c.id, out f) ? f : Family.Nenhuma;
    }

    public static string Name(Family f)
    {
        switch (f)
        {
            case Family.Fogo: return "FOGO";
            case Family.Agua: return "ÁGUA";
            case Family.Guerra: return "GUERRA";
            case Family.Fe: return "FÉ";
            case Family.Sinais: return "SINAIS";
            default: return "";
        }
    }

    public static Color Tint(Family f)
    {
        switch (f)
        {
            case Family.Fogo: return new Color(1f, 0.48f, 0.18f);
            case Family.Agua: return new Color(0.3f, 0.68f, 1f);
            case Family.Guerra: return new Color(0.82f, 0.84f, 0.9f);
            case Family.Fe: return new Color(1f, 0.88f, 0.4f);
            case Family.Sinais: return new Color(0.72f, 0.52f, 1f);
            default: return Color.gray;
        }
    }

    public static string Bonus(Family f, int level)
    {
        bool five = level >= 5;
        switch (f)
        {
            case Family.Fogo: return five ? "Inimigos que morrem queimando espalham o fogo" : "Seus golpes incendeiam os inimigos";
            case Family.Agua: return five ? "Fonte de Água Viva: +1 vida máxima e cura 1 a cada 45s" : "Seus golpes encharcam; molhados levam +20% de dano";
            case Family.Guerra: return five ? "+1 projétil e +1 perfuração" : "+15% de dano e +10% de crítico";
            case Family.Fe: return five ? "25% de chance de um milagre anular o dano" : "+1 nível de Escudo";
            case Family.Sinais: return five ? "A cada 15s um raio do céu cai em 3 inimigos" : "+1 raio em cadeia; o raio eletrifica";
            default: return "";
        }
    }
}

/// Gira o objeto em volta do eixo Y (usado pelas Lâminas Giratórias).
public class RunnerSpin : MonoBehaviour
{
    public float speed = 180f;
    void Update() { transform.Rotate(0f, speed * Time.deltaTime, 0f); }
}

/// Bate as asas (anjo destruidor).
public class RunnerFlap : MonoBehaviour
{
    public Transform left, right, body;
    float t;
    void Update()
    {
        t += Time.deltaTime;
        float f = Mathf.Sin(t * 6f) * 25f;
        if (left != null) left.localRotation = Quaternion.Euler(0f, 0f, 15f + f);
        if (right != null) right.localRotation = Quaternion.Euler(0f, 0f, -15f - f);
        if (body != null) body.localPosition = new Vector3(0f, 0.3f + Mathf.Sin(t * 3f) * 0.12f, 0f);
    }
}

/// <summary>
/// Evoluções: quando você junta as cartas certas, aparece uma carta EVOLUÇÃO garantida na próxima escolha.
/// </summary>
public class RunnerEvolution
{
    public string id, name, desc, verse;
    public string reqWeapon;                 // id da arma necessária (ou null)
    public string reqWeaponName;
    public string[] reqCards = new string[0];
    public int[] reqStacks = new int[0];
    public string[] reqNames = new string[0];
    public Action<RunnerGame> apply;
    RunnerCard card;

    public string ReqText
    {
        get
        {
            var parts = new List<string>();
            if (reqWeapon != null) parts.Add(reqWeaponName);
            for (int i = 0; i < reqCards.Length; i++) parts.Add(reqNames[i] + (reqStacks[i] > 1 ? " x" + reqStacks[i] : ""));
            return string.Join(" + ", parts);
        }
    }

    /// Quantos requisitos já foram cumpridos (para as dicas "quase evoluindo").
    public int Progress(RunnerGame g, out int total)
    {
        total = reqCards.Length + (reqWeapon != null ? 1 : 0);
        int ok = 0;
        if (reqWeapon != null && g.stats.weapon.id == reqWeapon) ok++;
        for (int i = 0; i < reqCards.Length; i++) if (g.CardStacks(reqCards[i]) >= reqStacks[i]) ok++;
        return ok;
    }

    public bool Ready(RunnerGame g)
    {
        int total;
        return Progress(g, out total) == total && g.CardStacks(id) == 0;
    }

    public RunnerCard Card
    {
        get
        {
            if (card == null)
            {
                var self = this;
                card = new RunnerCard
                {
                    id = id, name = name, rarity = Rarity.Lendario, maxStacks = 1, isEvolution = true,
                    desc = desc + " " + verse, reqText = ReqText,
                    cond = g => self.Ready(g),
                    apply = g =>
                    {
                        self.apply(g);
                        PlayerPrefs.SetInt("evo_" + self.id, 1);   // registro de descobertas
                        PlayerPrefs.Save();
                    }
                };
            }
            return card;
        }
    }
}

public static class Evolutions
{
    static List<RunnerEvolution> all;
    public static List<RunnerEvolution> All => all ?? (all = Build());

    static RunnerEvolution E(string id, string name, string desc, string verse, string weapon, string weaponName,
        string[] cards, int[] stacks, string[] names, Action<RunnerGame> apply)
    {
        return new RunnerEvolution
        {
            id = "evo_" + id, name = name, desc = desc, verse = verse, reqWeapon = weapon, reqWeaponName = weaponName,
            reqCards = cards, reqStacks = stacks, reqNames = names, apply = apply
        };
    }

    static void RebuildWeapon(RunnerGame g)
    {
        if (g.player != null && g.player.weaponModel != null) g.player.weaponModel.currentId = "";
    }

    static List<RunnerEvolution> Build()
    {
        var fire = new Color(1f, 0.5f, 0.1f);
        var l = new List<RunnerEvolution>();

        l.Add(E("espada", "Espada Flamejante do Querubim",
            "A espada vira fogo: +60% de dano, golpe 50% mais longo e largo, explode ao acertar e +1 lâmina giratória.", "(Gn 3:24)",
            "espada", "Espada", new[] { "laminas" }, new[] { 1 }, new[] { "Lâminas Giratórias" },
            g => { var w = g.stats.weapon; w.name = "Espada Flamejante"; w.damage *= 1.6f; w.reach *= 1.5f; w.arc *= 1.25f; w.explode += 1.5f; w.color = fire;
                   g.stats.orbitBlades++; g.RefreshOrbitBlades(); RebuildWeapon(g); }));

        l.Add(E("martelo", "Martelo e Estaca de Jael",
            "Cada pancada vira um terremoto: +40% de dano, golpe mais largo, explosão maior e 25% mais rápido.", "(Jz 4:21)",
            "martelo", "Martelo de Guerra", new[] { "pisao" }, new[] { 1 }, new[] { "Pisão do Querubim" },
            g => { var w = g.stats.weapon; w.name = "Martelo de Jael"; w.damage *= 1.4f; w.arc += 1f; w.explode += 1.5f; w.cooldown *= 0.75f; RebuildWeapon(g); }));

        l.Add(E("lanca", "Lança de Fineias",
            "Estocada 60% mais longa, +30% de dano e a onda atravessa +4 inimigos.", "(Nm 25:7-8)",
            "lanca", "Lança", new[] { "perfura" }, new[] { 2 }, new[] { "Perfurante" },
            g => { var w = g.stats.weapon; w.name = "Lança de Fineias"; w.reach *= 1.6f; w.damage *= 1.3f; w.pierce += 4; RebuildWeapon(g); }));

        l.Add(E("arco", "Arco de Jônatas",
            "\"O arco de Jônatas nunca voltava atrás\": 3 flechas por disparo, +2 perfuração e +20% de dano.", "(2Sm 1:22)",
            "arco", "Arco Longo", new[] { "polvora" }, new[] { 2 }, new[] { "Pólvora Extra" },
            g => { var w = g.stats.weapon; w.name = "Arco de Jônatas"; w.pellets += 2; w.spread = Mathf.Max(w.spread, 6f); w.pierce += 2; w.damage *= 1.2f; RebuildWeapon(g); }));

        l.Add(E("trombeta", "Trombeta do Juízo",
            "As trombetas tocam a cada 8s: derrubam muralhas, apagam o fogo inimigo e chamam fogo do céu sobre 3 inimigos.", "(Ap 8:7)",
            null, null, new[] { "jerico", "fogoceu" }, new[] { 1, 2 }, new[] { "Trombetas de Jericó", "Fogo do Céu" },
            g => g.stats.judgmentTrumpet = true));

        l.Add(E("pedra", "A Pedra que Derrubou Golias",
            "+100% de dano contra elites, lamassus e chefes, +15% de crítico e crítico x+1.", "(1Sm 17:49)",
            null, null, new[] { "funda", "crit" }, new[] { 3, 2 }, new[] { "Funda de Davi", "Olho de Águia" },
            g => { g.stats.slingLevel += 3; g.stats.critChance += 0.15f; g.stats.critMul += 1f; }));

        l.Add(E("pao", "O Pão da Vida",
            "Maná a cada 10 abates (+7% de dano) e a cada 3 porções você recupera 1 vida.", "(Jo 6:35)",
            null, null, new[] { "mana", "pao" }, new[] { 1, 2 }, new[] { "Maná do Céu", "Pão Diário" },
            g => g.stats.manaEternal = true));

        l.Add(E("coluna", "Coluna de Fogo e Nuvem",
            "Escudo recarrega muito mais rápido e, ao quebrar, solta uma onda que destrói tudo em volta.", "(Êx 13:21)",
            null, null, new[] { "escudo", "fantasma" }, new[] { 2, 1 }, new[] { "Escudo de Energia x2", "Reflexos" },
            g => { g.stats.shieldLevel += 2; g.stats.pillarOfFire = true; g.RechargeShieldNow(); }));

        l.Add(E("cruz", "Ressurreição",
            "+1 vida máxima. Uma vez por jogatina, ao morrer, você volta com todas as vidas.", "(1Co 15:55)",
            null, null, new[] { "coracao", "vampiro" }, new[] { 3, 1 }, new[] { "Coração Extra", "Vampirismo" },
            g => { g.stats.resurrection = true; g.maxLives++; g.Heal(1); }));

        l.Add(E("selos", "Os Sete Selos",
            "+2 projéteis, o raio pula em +3 inimigos e causa 60% a mais de dano.", "(Ap 6:1)",
            null, null, new[] { "multi", "corrente" }, new[] { 3, 1 }, new[] { "Tiro Múltiplo", "Corrente Elétrica" },
            g => { g.stats.extraProjectiles += 2; g.stats.chain += 3; g.stats.chainMul *= 1.6f; }));

        l.Add(E("vara", "Vara de Arão que Floresceu",
            "Chance de reduzir a pó sobe para 15% e o mar se abre sozinho a cada 25 abates.", "(Nm 17:8)",
            null, null, new[] { "vara", "marvermelho" }, new[] { 1, 1 }, new[] { "Vara de Moisés", "Abrir o Mar" },
            g => { g.stats.mosesChance = 0.15f; g.stats.aaronRod = true; }));

        return l;
    }
}
