(function(root){
'use strict';
// Guide-derived mechanisms, not an artifact power tier list. Values in core are
// native activation/upgrade targets used for diagnostics and equal-weight goals.
const profiles={};
const groups=['基础实验','物理武器','三元素','复合流派','其他流派','独立武器'];
function add(id,name,kind,category,sourceIndex,core={},options={}){
 profiles[id]={id,name,short:name.split(' · ')[0],kind,category,sourceIndex,core,low:{},group:groups[2],element:'FIRE',speed:0,mode:'basic',description:'',sources:[sourceIndex],...options,source:'https://sephiriadfm05.streamlit.app/build_analysis'};
 return profiles[id];
}
add('balanced','自由实验 · 均衡','balanced',null,12,{}, {group:groups[0],description:'对已建模的伤害通道取几何平均；适合检查布局规则。专门构筑请选择对应模型。'});
for(const [slug,name,weapon,index] of [['sword','剑盾',0,16],['greatsword','大剑',1,17],['dagger','匕首',2,18],['crossbow','弩',3,19],['katana','武士刀',5,20],['staff','长棍',7,21]]){
 add('physical-'+slug,name+' · 普攻攻速','weapon','STURDY',index,{1262:4},{group:groups[1],element:'PHYSICAL',speed:1,weapon,mode:'basic',description:'计算物理面板、普攻、武器伤害、攻速与期望暴击。末击占比和攻击频率可按武器调整。',sources:[index,7,8,15]});
 add('special-'+slug,name+' · 特殊攻击','weapon','STURDY',index,{1221:5},{group:groups[1],element:'PHYSICAL',speed:slug==='katana'?1:0,weapon,mode:'special',description:'按特殊攻击计算收益；普攻加成不计入。持续输出受设定的 MP 消耗约束。',mpUse:slug==='katana'?0:8,sources:[index,8,14,15]});
}
add('dash','物理 · 冲刺攻击','weapon','STURDY',20,{1011:3},{group:groups[1],element:'PHYSICAL',mode:'dash',description:'风草围巾、冲刺伤害和冲刺恢复参与计算，普通攻击伤害不计入。',sources:[16,18,20,21]});
add('crown','物理 · 末击双冠','weapon','STURDY',20,{1080:4,1025:4},{group:groups[1],element:'PHYSICAL',speed:0,lastShare:1,description:'末击占比默认 100%，适合飞柳等末击玩法；贝鲁特之镰开启暴击溢出转处决。',sources:[17,19,20,21]});
add('deer','无暴 · 坚固风之歌','weapon','STURDY',35,{1262:4,1181:7},{group:groups[4],element:'PHYSICAL',speed:1,noCrit:true,description:'这是主动舍弃暴击的构筑策略，暴击与暴伤不计收益；保留神器的其他属性和负面效果。',sources:[35,36,7,15]});
add('ember','余烬 · 持续灼烧','burn','EMBER',22,{1213:3,1128:4,1211:5},{description:'分别计算上灼烧速度、层数上限、单跳伤害和跳速；等离子转化将改变结算机制。'});
add('blue-burn','霜灼 · 冰火转化','burn','EMBER',22,{1243:0,1213:3,1177:4},{group:groups[3],element:'ICE',description:'高属性全额、低属性 25% 参与灼烧；实际伤害属性随冰火高低切换。',sources:[22,24,36]});
add('glacier','冰川 · 冻伤冻结','freeze','GLACIER',24,{1145:3,1180:3,1212:5},{element:'ICE',description:'根据冻伤施加频率、蓝爪概率与冻结层数门槛计算；冰海鸥之足的战斗叠层只作提示。'});
add('shock','魔法科技 · 触电','shock','MAGITECH',26,{1167:3,1182:4,1238:5},{element:'LIGHTNING',description:'区分定时结算和指南针强化触电；项链延长时间不会无条件提高普通结算频率。'});
add('plasma','等离子 · 触电灼烧','plasma','MAGITECH',26,{1294:0,1167:3,1128:4,1182:4},{group:groups[3],element:'LIGHTNING',description:'同时建模灼烧与触电层数、红线球和指南针。转化公式以本机游戏实现为准。',sources:[22,26,39]});
add('sun','太阳剑 · 自动回收','sun','FLAMESWORD',23,{1236:0,1229:3,1240:6},{description:'太阳剑次数、暴击和伤害单独计算；陨铁耳环零级就提供回收机制，升级只增加自身属性。'});
add('sun-return','太阳剑 · 护肩回收','sun','FLAMESWORD',23,{1237:4},{group:groups[3],returnMode:true,description:'按护肩冷却和回收路径命中率估计循环；与耳环同带时提示回收方式冲突。'});
add('frost','冰霜武具 · 剑鞘','frost','FROST',25,{1014:6,1179:6,1210:5},{element:'ICE',receiver:1014,description:'按原生倍率与充能时间计算，保留冰之翼 3/6 级和冰星 5 级的额外触发门槛。'});
add('frost-hammer','冰霜武具 · 暴风雪之锤','frost','FROST',25,{1208:5,1179:6},{element:'ICE',receiver:1208,description:'以暴风雪之锤为武器触发目标，冲刺触发频率取场景设置；金色针必须指向可造成伤害的神器。'});
add('frost-spear','冰霜武具 · 沃尔斯帕','frost','FROST',25,{1137:5,1179:6},{element:'ICE',receiver:1137,description:'沃尔斯帕按实际等级的发射数量与倍率一起计算，避免只看等级或单发倍率。'});
add('frost-lake','冰武湖泊 · 水滴','frost-lake','FROST',25,{1248:0,1247:1},{group:groups[3],element:'ICE',description:'水滴使用冰属性 × 6% × 追加 MP × 50%；追加 MP 扣除本机常量中的基础 50 点。充能额外次数不增加水滴本体伤害。',sources:[25,30,36]});
add('cloud','暴风云 · 叶纹皮','cloud','DARKCLOUD',27,{1072:3,1104:4,1129:5},{element:'LIGHTNING',description:'按乌云库存、回复、消耗和叶纹皮触发建模；云种箭头强化概率在 100% 封顶。'});
add('companion-cloud','同伴乌云 · 同排转化','companion-cloud','DARKCLOUD',27,{1263:0,1264:0,1071:4},{group:groups[3],element:'LIGHTNING',description:'同伴的攻击频率用于触发乌云；奉献徽章只转化同一行的同伴。以乌云输出为目标。',sources:[27,28]});
add('companion','同伴 · 暴击继承','companion','COMPANION',28,{1249:3,1130:7,1090:6},{group:groups[4],element:null,description:'同伴使用自身基础伤害；战术指南书开启玩家暴击继承，四属性面板不直接增加同伴基础伤害。'});
add('planet','行星 · 银河望远镜','planet','PLANET',29,{1251:2,1108:0},{group:groups[4],element:null,speed:1,description:'按每颗星球原生伤害、攻击间隔与发射数计算；银河必须 2 级，望远镜覆盖八邻格。'});
add('planet-fixed','行星 · 迷你机枪','planet','PLANET',29,{1251:2,1108:0},{group:groups[4],element:null,speed:0,weapon:3,actions:10,attackWeight:.25,mpUse:10,description:'银河以动作次数触发，普通攻击基础权重与特攻频率分开；此分支不把攻速转成额外动作。'});
add('burn-planet','灼星 · 单跳借伤','planet','PLANET',29,{1295:0,1251:2,1108:0,1128:4},{group:groups[3],element:'FIRE',burnPlanet:true,speed:1,description:'日志只添加一层灼烧单跳伤害 × 星球稀有度次数；不受灼烧层数或跳速加成。',sources:[29,22]});
add('true-damage','固伤 · 高频附伤','true','WINDSONG',31,{1267:14,1245:0,1251:2},{group:groups[4],element:null,noCrit:true,speed:1,weapon:3,actions:6.7,description:'无视防御伤害在普通伤害乘区之后相加。此策略只优化固伤 × 命中频率，星球升级伤害不计收益。'});
add('lake-release','湖泊 · 魔法剑解放','lake','LAKE',30,{1273:1,1124:2},{group:groups[4],element:'HIGHEST',mode:'special',weapon:2,lakeMode:'release',description:'按 MP 投射物数量和最大 MP 的阶梯门槛估算释放；当前 MP 比例是场景参数。'});
add('lake-missile','湖泊 · 制导模块','lake','LAKE',30,{1124:2,1278:4,1221:5},{group:groups[4],element:'FIRE',mode:'special',weapon:3,lakeMode:'missile',description:'追加 MP 驱动特殊攻击；火属性、武器和特殊攻击各自参与，不按湖泊神器名称统一加权。'});
add('lake-adama','湖泊 · 亚达玛飞刀','lake','LAKE',30,{1292:4,1124:2,1168:2},{group:groups[4],element:null,weapon:5,lakeMode:'adama',description:'飞刀依赖空缺 MP；回蓝既维持施法，也可能压低飞刀伤害，需要设置当前 MP 比例。',sources:[30,32]});
for(const [slug,element,cat,spell] of [['fire','FIRE','EMBER',3013],['ice','ICE','GLACIER',3020],['lightning','LIGHTNING','MAGITECH',3034]]){
 add('magic-'+slug,({fire:'火焰',ice:'冰霜',lightning:'闪电'})[slug]+' · 魔法书','magic',cat,32,{[spell]:4,1168:2,1165:2},{group:groups[4],element,description:'按已拥有法术的倍率、冷却和 MP 消耗计算；沙漏只加速右侧魔法书。',sources:[32,11]});
 add('double-'+slug,({fire:'火焰',ice:'冰霜',lightning:'闪电'})[slug]+' · 双发魔法书','magic',cat,32,{1292:4,[spell]:4,1168:2},{group:groups[3],element,double:true,description:'浇水壶以当前等级对应的最大 MP 门槛生效；不把 200 MP 当成所有等级的固定门槛。',sources:[32,11,30]});
}
add('guardian','守护 · 防御转伤','weapon','GUARDIAN',33,{1053:3,1276:4},{group:groups[4],element:'PHYSICAL',mode:'special',defenseRatio:.005,description:'防御既参与生存效用，也按分支系数转成伤害。默认采用岩石剑的每点防御 0.5%。'});
add('armor-katana','守护 · 破甲刀','weapon','GUARDIAN',33,{1276:4,1217:4},{group:groups[4],element:'PHYSICAL',weapon:5,defenseRatio:.025,description:'采用攻略描述的防御转伤分支：每点防御 2.5%，与末击加成合并处理。',sources:[20,33]});
add('guardian-wind','守护风歌 · 岩象','guardian','GUARDIAN',33,{1246:4,1276:4},{group:groups[3],element:'PHYSICAL',speed:1,description:'岩象以防御为伤害基础，额外攻速每满 15% 增加一支矛，冲刺减少冷却。',sources:[33,7]});
add('shadow','影子 · 无形之舞','weapon','SHADOW',34,{1155:3,1077:2},{group:groups[4],element:'PHYSICAL',weapon:7,speed:1,evasionRatio:.01,description:'闪避用于额外攻击概率，并保留暴击和普攻伤害；无敌循环受敌人攻击影响，只作提示。'});
add('shadow-fury','影子 · 无尽沉浸','weapon','SHADOW',34,{1147:2,1058:2},{group:groups[4],element:'PHYSICAL',weapon:2,mode:'special',evasionRatio:.0125,description:'采用充能速度随闪避增加的机制；羽毛必须到 2 级才额外获得专注。',sources:[18,34]});
add('judge','裁判官 · 多减益爆炸','judge','PRECISION',37,{1139:0,1213:3,1183:3,1221:5},{group:groups[5],element:'CHAOS',weapon:1,mode:'special',description:'爆炸按减益层数的固定伤害计算，不吃四属性面板；计入特殊攻击、武器伤害及混沌属性的暴击加成。',sources:[37,8,14]});
add('nebolax','内波拉克斯 · 云瓶','cloud','DARKCLOUD',38,{1035:3,1072:3,1129:5},{group:groups[5],element:'LIGHTNING',weapon:7,bottle:true,description:'云瓶数量决定武器触发次数；该直接触发不吃乌云 10 的双点射，回复与消耗分别计算。',sources:[38,27]});
add('plasma-dagger','炙热之刃 · 等离子','plasma','MAGITECH',39,{1294:0,1167:3,1182:4,1128:4},{group:groups[5],element:'LIGHTNING',weapon:2,application:5,mpUse:8,description:'高频特攻施加等离子，按项链、指南针、层数和火雷面板计算；保留作者不同开局建议。',sources:[39,26,22]});
add('library-katana','图书馆第 2 型 · 普攻冲刺','weapon','STURDY',40,{1011:3,1149:2,1279:3},{group:groups[5],element:'PHYSICAL',weapon:5,mode:'hybrid',description:'普攻与冲刺按场景中的占比估算，冲刺次数参与灵体攻击；保留主站与作者专文的不同操作建议。',sources:[40,20]});
for(const [slug,element,cat,spell] of [['fire','FIRE','EMBER',3002],['ice','ICE','GLACIER',3027],['lightning','LIGHTNING','MAGITECH',3011],['tri','TRI','ELEMENTAL',3002]]){
 add('fusion-'+slug,'精灵融合杖 · '+({fire:'火弹',ice:'冰弹',lightning:'雷弹',tri:'三元素'})[slug],'magic',cat,41,{[spell]:1,1168:2,1062:5,1073:0},{group:groups[5],element,weapon:7,fusion:true,speed:1,actions:5.5,description:'只释放三种基础魔弹；吃普攻加成但不吃武器伤害。三元素分支需要保持属性相等才能共享最高属性加成。',sources:[41,11,32]});
}
add('fusion-sun','精灵融合杖 · 书库之阳','sun','FLAMESWORD',41,{1311:4,1236:0,1168:2,3002:1},{group:groups[5],element:'FIRE',weapon:7,bookSun:true,fusion:true,actions:5.5,description:'魔弹提供太阳剑触发；书库之阳在 0/2/4 级分别提供转化、额外 1 次、额外 2 次触发。',sources:[41,23,32]});
add('adama-sun','亚达玛 · 书库之阳','sun','FLAMESWORD',42,{1311:4,1236:0,1168:2,3002:1},{group:groups[5],element:'FIRE',weapon:5,bookSun:true,adama:true,description:'小法术与飞刀共同触发太阳剑，法术伤害转太阳剑伤害；默认按专文的简化分支构筑。',sources:[42,32,23]});
add('adama-sun-double','亚达玛 · 双发书库','sun','FLAMESWORD',42,{1311:4,1236:0,1168:2,1292:4},{group:groups[5],element:'FIRE',weapon:5,bookSun:true,adama:true,double:true,description:'在简化书库分支上加入浇水壶门槛。高 MP 与施法循环需要同时满足，背包压力更大。',sources:[42,32,30]});
const api={profiles,groups};if(typeof module!=='undefined'&&module.exports)module.exports=api;else root.SephiriaBuilds=api;
})(typeof window!=='undefined'?window:globalThis);
