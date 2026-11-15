from event_spool import EventSpool


def test_delivery_survives_restart_and_ack(tmp_path):
    path=tmp_path/"events.sqlite3"
    spool=EventSpool(path)
    payload={"eventKey":"event-one", "plate":"51H12345"}
    assert spool.observe(1,"51H12345",payload,now=100)
    spool.close()
    spool=EventSpool(path)
    assert spool.first(1)==("event-one",payload)
    spool.acknowledge("event-one")
    assert spool.first(1) is None
    # The same standing vehicle does not re-emit after worker restart.
    assert not spool.observe(1,"51H12345",{"eventKey":"event-two"},now=101)
    assert spool.first(1) is None
    spool.close()


def test_presence_rearms_after_absence_and_is_per_camera(tmp_path):
    spool=EventSpool(tmp_path/"events.sqlite3")
    assert spool.observe(1,"51H12345",{"eventKey":"a"},now=100)
    assert not spool.observe(1,"51H12345",{"eventKey":"b"},now=110)
    assert spool.observe(1,"51H12345",{"eventKey":"c"},now=130)
    assert spool.observe(2,"51H12345",{"eventKey":"d"},now=131)
    assert spool.first(2)[0]=="d"
    spool.close()
