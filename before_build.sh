#!/bin/bash
(git rev-parse --short HEAD 2>/dev/null || echo dev) > ./resources/commit
