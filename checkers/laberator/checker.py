#!/usr/bin/python3
from base64 import b64encode
from hashlib import sha1
import json
import pickle
from sys import argv, stderr
import socket
import traceback

import requests
import websocket

from generators import generate_headers, generate_login, generate_password, generate_label

REGISTER_URL = "http://{hostport}/register?login={login}&password={password}"
LOGIN_URL = "http://{hostport}/login?login={login}&password={password}"
WS_URL = "ws://{}:{}/cmdexec"
OK, CORRUPT, MUMBLE, DOWN, CHECKER_ERROR = 101, 102, 103, 104, 110
PORT = 8080


def print_to_stderr(*objs):
    print(*objs, file=stderr)


def get_hash(obj):
    return b64encode(sha1(pickle.dumps(obj)).digest()).decode()


def signup(hostport, login, password):
    register_url = REGISTER_URL.format(
        hostport=hostport,
        login=login,
        password=password
    )
    r = requests.get(
        url=register_url,
        headers=generate_headers(),
        timeout=10
    )
    r.raise_for_status()
    return r.cookies


def signin(hostport, login, password):
    login_url = LOGIN_URL.format(
        hostport=hostport,
        login=login,
        password=password
    )
    r = requests.get(
        url=login_url,
        headers=generate_headers(),
        timeout=10
    )
    r.raise_for_status()
    return r.cookies


def create_command_request(command, data):
    return json.dumps({
        "Command": command,
        "Data": json.dumps(data)
    })


def get_raw_cookies(cookies):
    return "; ".join([str(k) + "=" + str(v) for k, v in cookies.items()])


def info():
    print("vulns: 1")
    exit(OK)


def check(hostname):
    exit(OK)


def not_found(*args):
    print_to_stderr("Unsupported command %s" % argv[1])
    return CHECKER_ERROR


def create_label(ws, cookies, text, font, size):
    ws.send(create_command_request("create", {
        "RawCookies": get_raw_cookies(cookies),
        "Text": text,
        "Font": font,
        "Size": size,
    }))
    return ws.recv()


def list_labels(ws, cookies):
    ws.send(create_command_request("list", {
        "RawCookies": get_raw_cookies(cookies),
        "Offset": 0
    }))
    response = ws.recv()
    print_to_stderr("ws response:", response)
    return json.loads(response.encode())


def put(hostname, flag_id, flag, vuln):
    login = generate_login()
    password = generate_password()
    exit_code = OK
    try:
        cookies = signup("{}:{}".format(hostname, PORT), login, password)
        label_font, label_size = generate_label()
        ws = websocket.create_connection(
            WS_URL.format(hostname, PORT),
            timeout=10
        )
        if create_label(ws, cookies, flag, label_font, label_size) != "true":
            print_to_stderr("Can not create label")
            exit(MUMBLE)
        ws.send(create_command_request("list", {
            "RawCookies": get_raw_cookies(cookies),
            "Offset": 0
        }))
        print("{},{},{}".format(
            login,
            password,
            get_hash((flag, label_font, label_size))
        ))
        ws.close()
    except (requests.exceptions.ConnectTimeout, socket.timeout, requests.exceptions.ConnectionError):
        traceback.print_exc()
        exit_code = DOWN
    except (
        requests.exceptions.HTTPError, UnicodeDecodeError, json.decoder.JSONDecodeError,
        TypeError, websocket._exceptions.WebSocketBadStatusException,
        websocket._exceptions.WebSocketConnectionClosedException,
        requests.exceptions.ReadTimeout
    ):
        traceback.print_exc()
        exit_code = MUMBLE
    exit(exit_code)


def get(hostname, flag_id, flag, _):
    login, password, expected_label_hash = flag_id.split(',')
    exit_code = OK
    try:
        cookies = signin("{}:{}".format(hostname, PORT), login, password)
        ws = websocket.create_connection(
            WS_URL.format(hostname, PORT),
            timeout=10
        )
        labels = list_labels(ws, cookies)
        if len(labels) != 1:
            print_to_stderr("There is a multiple labels={} gotten by ws={}, cookies={}".format(labels, ws, cookies))
            exit(CORRUPT)
        label = labels[0]
        text = label.get("Text", None)
        font = label.get("Font", None)
        size = label.get("Size", None)
        if text is None or font is None or size is None:
            print_to_stderr("Label text =", text)
            print_to_stderr("Label font =", font)
            print_to_stderr("Label size =", size)
            exit(MUMBLE)
        real_label_hash = get_hash((text, font, size))
        if real_label_hash != expected_label_hash:
            print_to_stderr("Label(text={}, font={}, size={}) real hash='{}', but expected hash='{}'".format(
                text, font, size, real_label_hash, expected_label_hash
            ))
            exit(CORRUPT)
        if text != flag:
            print_to_stderr("Label(text={}, font={}, size={}), but expected text(flag)='{}'".format(
                text, font, size, flag
            ))
            exit(CORRUPT)
    except (requests.exceptions.ConnectTimeout, socket.timeout, requests.exceptions.ConnectionError):
        traceback.print_exc()
        exit_code = DOWN
    except (
        requests.exceptions.HTTPError, UnicodeDecodeError, json.decoder.JSONDecodeError,
        TypeError, websocket._exceptions.WebSocketBadStatusException,
        websocket._exceptions.WebSocketConnectionClosedException,
        requests.exceptions.ReadTimeout, KeyError
    ):
        traceback.print_exc()
        exit_code = MUMBLE
    exit(exit_code)


COMMANDS = {'check': check, 'put': put, 'get': get, 'info': info}


def main():
    try:
        COMMANDS.get(argv[1], not_found)(*argv[2:])
    except Exception:
        traceback.print_exc()
        exit(CHECKER_ERROR)


if __name__ == '__main__':
    main()