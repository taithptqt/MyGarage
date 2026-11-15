const test=require('node:test');
const assert=require('node:assert/strict');
const {createTracker}=require('./tracking.js');
const row=(extra={})=>({track_id:1,box:[100,100,200,140],text:'30A12345',observations:3,velocity:[20,0],uncertain:false,...extra});
const result=(time,details=[row()],extra={})=>({time,details,width:1280,height:720,...extra});

test('one missed detection and a 400ms inference gap do not flash the label off',()=>{
  const tracker=createTracker();tracker.update(result(1),1,1);
  const key=tracker.sample(1,1)[0].displayKey;
  tracker.update(result(1.1,[]),1.15,1.15);
  assert.equal(tracker.sample(1.4,1.4)[0].displayKey,key);
  tracker.update(result(1.45),1.45,1.45);
  assert.equal(tracker.sample(1.45,1.45)[0].displayKey,key);
});

test('empty updates never extend a disappeared plate beyond the coast limit',()=>{
  const tracker=createTracker();tracker.update(result(1),1,1);
  for(const time of [1.1,1.3,1.6,1.7])tracker.update(result(time,[]),time,time);
  assert.equal(tracker.sample(1.7,1.7).length,0);
});

test('new geometry blends from the previous display instead of jumping',()=>{
  const tracker=createTracker();tracker.update(result(0,[row({velocity:[0,0]})]),0,0);
  tracker.update(result(.1,[row({box:[120,100,220,140],velocity:[0,0]})]),.1,.1);
  assert.equal(tracker.sample(.1,.1)[0].box[0],100);
  assert.ok(Math.abs(tracker.sample(.1,.15)[0].box[0]-110)<1e-8);
  assert.equal(tracker.sample(.1,.21)[0].box[0],120);
});

test('seek, resolution change and far reappearance cannot retain a stale track',()=>{
  const tracker=createTracker();tracker.update(result(3),3,3);
  tracker.update(result(1,[]),1,4);assert.equal(tracker.sample(1,4).length,0);
  tracker.update(result(2),2,5);
  tracker.update(result(2.1,[],{width:1920}),2.1,5.1);assert.equal(tracker.sample(2.1,5.1).length,0);
  tracker.update(result(4),4,6);tracker.clear();assert.equal(tracker.sample(4,6).length,0);
  tracker.update(result(5,[row({track_id:2,box:[900,100,1000,140]})]),5,7);
  assert.equal(tracker.sample(5,7).length,1);
});

test('short ID churn retains one nearby label but separate vehicles remain separate',()=>{
  const tracker=createTracker();tracker.update(result(0),0,0);
  const key=tracker.sample(0,0)[0].displayKey;
  tracker.update(result(.1,[row({track_id:2})]),.1,.1);
  assert.equal(tracker.sample(.1,.1).length,1);
  assert.equal(tracker.sample(.1,.1)[0].displayKey,key);
  tracker.update(result(.2,[row({track_id:2}),row({track_id:3,box:[900,100,1000,140]})]),.2,.2);
  assert.equal(tracker.sample(.2,.2).length,2);
});

test('confidence near threshold does not alternate warning state every frame',()=>{
  const tracker=createTracker();tracker.update(result(0,[row({uncertain:true})]),0,0);
  for(const time of [.1,.2]){tracker.update(result(time),time,time);assert.equal(tracker.sample(time,time)[0].uncertain,true)}
  tracker.update(result(.3),.3,.3);assert.equal(tracker.sample(.3,.3)[0].uncertain,false);
});

test('the actual video painter reuses SVG nodes across detections and brief misses',()=>{
  const vm=require('node:vm'),fs=require('node:fs'),path=require('node:path');
  class Element{
    constructor(){this.children=[];this.style={};this.attributes={};this.textContent=''}
    setAttribute(key,value){this.attributes[key]=value}
    toggleAttribute(key,on){if(on)this.attributes[key]='';else delete this.attributes[key]}
    append(...nodes){for(const node of nodes){node.parent=this;this.children.push(node)}}
    remove(){this.parent.children=this.parent.children.filter(node=>node!==this)}
    addEventListener(){}
  }
  const elements=new Map(),get=id=>{if(!elements.has(id))elements.set(id,new Element());return elements.get(id)};
  Object.assign(get('video'),{readyState:2,currentTime:1,videoWidth:1280,videoHeight:720,clientWidth:1280,clientHeight:720,offsetLeft:0,offsetTop:0});
  let now=1;
  const context=vm.createContext({document:{getElementById:get,createElementNS:()=>new Element()},window:{addEventListener(){}},
    plateTracking:require('./tracking.js'),performance:{now:()=>now*1000},requestAnimationFrame:()=>1,cancelAnimationFrame(){}});
  vm.runInContext(fs.readFileSync(path.join(__dirname,'web.js'),'utf8'),context);
  context.input=result(1);
  vm.runInContext("mode='file';latest=input;paint()",context);
  const original=get('overlay').children.slice();assert.equal(original.length,2);
  for(const [time,details]of [[1.1,[row()]],[1.2,[]],[1.3,[row()]]]){
    now=time;get('video').currentTime=time;context.input=result(time,details);
    vm.runInContext('latest=input;paint()',context);
    assert.deepEqual(get('overlay').children,original);
    assert.equal(get('overlay').attributes.hidden,undefined);
  }
  now=2;get('video').currentTime=2;vm.runInContext('paint()',context);
  assert.equal(get('overlay').children.length,0);
  vm.runInContext('cancel();paint()',context);assert.equal(get('overlay').children.length,0);
});
